using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Contracts;

namespace SalekhPos.SystemAdministration.Api;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform").RequireAuthorization().RequireRateLimiting("business");
        group.MapGet("/authority", async (HttpContext context, SuperAdminAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.GetAuthorityAsync(PlatformMfaPolicy.Identity(context.User), cancellationToken)));
        group.MapGet("/super-admins", async (HttpContext context, SuperAdminAdministration administration,
            CancellationToken cancellationToken) =>
        {
            var page = Page(context);
            if (page is null) return InvalidInput();
            return Results.Ok(await administration.ListAsync(PlatformMfaPolicy.Identity(context.User),
                page.Value.Size, page.Value.After, cancellationToken));
        });
        group.MapGet("/authority-audit", async (HttpContext context, SuperAdminAdministration administration,
            CancellationToken cancellationToken) =>
        {
            var page = Page(context);
            if (page is null) return InvalidInput();
            return Results.Ok(await administration.ListAuditAsync(PlatformMfaPolicy.Identity(context.User),
                page.Value.Size, page.Value.After, cancellationToken));
        });
        group.MapPost("/super-admins", async (RegisterSuperAdminRequest request, HttpContext context,
            PlatformMfaPolicy policy, SuperAdminAdministration administration, CancellationToken cancellationToken) =>
        {
            var actor = policy.RequireActor(context.User);
            try { return Results.Ok(await administration.RegisterAsync(actor, request, context.TraceIdentifier, cancellationToken)); }
            catch (ArgumentException) { return InvalidInput(); }
        });
        group.MapPost("/super-admins/{adminId:guid}/revoke", async (Guid adminId, RevokeSuperAdminRequest request,
            HttpContext context, PlatformMfaPolicy policy, SuperAdminAdministration administration, CancellationToken cancellationToken) =>
        {
            var actor = policy.RequireActor(context.User);
            try { return Results.Ok(await administration.RevokeAsync(actor, adminId, request, context.TraceIdentifier, cancellationToken)); }
            catch (ArgumentException) { return InvalidInput(); }
        });
    }

    private static (int Size, Guid? After)? Page(HttpContext context)
    {
        var size = 50;
        Guid? after = null;
        if (context.Request.Query.TryGetValue("pageSize", out var rawSize)
            && (!int.TryParse(rawSize, out size) || size is < 1 or > 100)) return null;
        if (context.Request.Query.TryGetValue("after", out var rawAfter))
        {
            if (!Guid.TryParse(rawAfter, out var parsed) || parsed == Guid.Empty) return null;
            after = parsed;
        }
        return (size, after);
    }

    private static IResult InvalidInput() => Results.Problem(statusCode: 400, title: "Invalid platform operation",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_platform_operation" });
}
