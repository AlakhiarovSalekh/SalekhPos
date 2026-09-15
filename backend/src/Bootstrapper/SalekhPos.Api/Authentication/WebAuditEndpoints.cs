using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Audit.Application.AuditTrail;

namespace SalekhPos.Api.Authentication;

public static class WebAuditEndpoints
{
    public static void MapWebAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1/organizations/{organizationId:guid}/audit-events")
            .AllowAnonymous().RequireRateLimiting("business");
        group.MapGet("", async (Guid organizationId, HttpContext context, WebAuthenticationState state,
            IAuditTrail audit, CancellationToken ct) =>
        {
            var identity = await Identity(context, state);
            if (identity is null) return Unauthenticated(state);
            if (!TryQuery(context, out var size, out var after, out var action, out var branchId)) return Invalid();
            return Results.Ok(await audit.ListAsync(identity, organizationId, size, after, action, branchId, ct));
        });
        group.MapGet("/verify", async (Guid organizationId, HttpContext context, WebAuthenticationState state,
            IAuditTrail audit, CancellationToken ct) =>
        {
            var identity = await Identity(context, state);
            if (identity is null) return Unauthenticated(state);
            if (!TryVerify(context, out var from, out var limit)) return Invalid();
            return Results.Ok(await audit.VerifyAsync(identity, organizationId, from, limit, ct));
        });
    }

    private static async Task<AuditIdentity?> Identity(HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray(); var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try { var value = new AuditIdentity(issuers[0].Value, subjects[0].Value); value.Validate(); return value; }
        catch (ArgumentException) { return null; }
    }

    private static bool TryQuery(HttpContext context, out int size, out long? after,
        out string? action, out Guid? branchId)
    {
        size = 50; after = null; action = null; branchId = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "afterSequence"
            and not "action" and not "branchId")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out size)
                || size is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("afterSequence", out var cursors))
        {
            if (cursors.Count != 1 || !long.TryParse(cursors[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 0) return false;
            after = value;
        }
        if (context.Request.Query.TryGetValue("action", out var actions))
        {
            if (actions.Count != 1) return false;
            var value = actions[0]?.Trim();
            if (string.IsNullOrEmpty(value) || value.Length > 120 || value.Any(char.IsControl)) return false;
            action = value;
        }
        if (context.Request.Query.TryGetValue("branchId", out var branches))
        {
            if (branches.Count != 1 || !Guid.TryParseExact(branches[0], "D", out var value)
                || value == Guid.Empty) return false;
            branchId = value;
        }
        return true;
    }
    private static bool TryVerify(HttpContext context, out long? fromSequence, out int limit)
    {
        fromSequence = null; limit = 1000;
        if (context.Request.Query.Keys.Any(key => key is not "fromSequence" and not "limit")) return false;
        if (context.Request.Query.TryGetValue("fromSequence", out var from))
        {
            if (from.Count != 1 || !long.TryParse(from[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 1) return false;
            fromSequence = value;
        }
        if (context.Request.Query.TryGetValue("limit", out var limits)
            && (limits.Count != 1 || !int.TryParse(limits[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                || limit is < 1 or > 5000)) return false;
        return true;
    }

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();
    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The audit request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_audit_request" });
}
