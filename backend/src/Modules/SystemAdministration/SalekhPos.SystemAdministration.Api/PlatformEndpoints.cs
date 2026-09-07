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

    private static IResult InvalidInput() => Results.Problem(statusCode: 400, title: "Invalid platform operation",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_platform_operation" });
}
