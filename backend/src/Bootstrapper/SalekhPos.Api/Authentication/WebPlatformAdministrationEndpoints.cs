using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using SalekhPos.SystemAdministration.Api;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Contracts;

namespace SalekhPos.Api.Authentication;

public static class WebPlatformAdministrationEndpoints
{
    public static void MapWebPlatformAdministrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1/platform").AllowAnonymous().RequireRateLimiting("business");
        group.MapGet("/authority", Authority);
        group.MapGet("/super-admins", ListAdmins);
        group.MapGet("/authority-audit", ListAudit);
        group.MapPost("/super-admins", Register);
        group.MapPost("/super-admins/{adminId:guid}/revoke", Revoke);
    }

    private static async Task<IResult> Authority(HttpContext context, WebAuthenticationState state,
        SuperAdminAdministration administration, CancellationToken cancellationToken)
    {
        var principal = await Principal(context, state);
        if (principal is null) return Unauthorized(state);
        return Results.Ok(await administration.GetAuthorityAsync(PlatformMfaPolicy.Identity(principal), cancellationToken));
    }

    private static async Task<IResult> ListAdmins(HttpContext context, WebAuthenticationState state,
        SuperAdminAdministration administration, CancellationToken cancellationToken)
    {
        var principal = await Principal(context, state);
        if (principal is null) return Unauthorized(state);
        var page = Page(context);
        if (page is null) return Invalid();
        return Results.Ok(await administration.ListAsync(PlatformMfaPolicy.Identity(principal),
            page.Value.Size, page.Value.After, cancellationToken));
    }

    private static async Task<IResult> ListAudit(HttpContext context, WebAuthenticationState state,
        SuperAdminAdministration administration, CancellationToken cancellationToken)
    {
        var principal = await Principal(context, state);
        if (principal is null) return Unauthorized(state);
        var page = Page(context);
        if (page is null) return Invalid();
        return Results.Ok(await administration.ListAuditAsync(PlatformMfaPolicy.Identity(principal),
            page.Value.Size, page.Value.After, cancellationToken));
    }

    private static async Task<IResult> Register(RegisterSuperAdminRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery, PlatformMfaPolicy policy,
        SuperAdminAdministration administration, CancellationToken cancellationToken)
    {
        var principal = await Principal(context, state);
        if (principal is null) return Unauthorized(state);
        if (!await WebAuthentication.ValidateMutation(context, antiforgery, state.Settings!)) return Invalid();
        return Results.Ok(await administration.RegisterAsync(policy.RequireActor(principal), request,
            context.TraceIdentifier, cancellationToken));
    }

    private static async Task<IResult> Revoke(Guid adminId, RevokeSuperAdminRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery, PlatformMfaPolicy policy,
        SuperAdminAdministration administration, CancellationToken cancellationToken)
    {
        var principal = await Principal(context, state);
        if (principal is null) return Unauthorized(state);
        if (!await WebAuthentication.ValidateMutation(context, antiforgery, state.Settings!)) return Invalid();
        return Results.Ok(await administration.RevokeAsync(policy.RequireActor(principal), adminId, request,
            context.TraceIdentifier, cancellationToken));
    }

    private static async Task<ClaimsPrincipal?> Principal(HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        return result.Succeeded ? result.Principal : null;
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

    private static IResult Unauthorized(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "Invalid platform operation",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_platform_operation" });
}
