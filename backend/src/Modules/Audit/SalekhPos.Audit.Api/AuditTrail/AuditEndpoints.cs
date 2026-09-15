using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.Audit.Application.AuditTrail;

namespace SalekhPos.Audit.Api.AuditTrail;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/audit-events")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", async (Guid organizationId, HttpContext context, IAuditTrail audit, CancellationToken ct) =>
        {
            if (!TryQuery(context, out var pageSize, out var after, out var action, out var branchId)) return Invalid();
            return Results.Ok(await audit.ListAsync(Identity(context), organizationId, pageSize, after, action, branchId, ct));
        });
        group.MapGet("/verify", async (Guid organizationId, HttpContext context, IAuditTrail audit, CancellationToken ct) =>
        {
            if (!TryVerify(context, out var from, out var limit)) return Invalid();
            return Results.Ok(await audit.VerifyAsync(Identity(context), organizationId, from, limit, ct));
        });
        group.MapGet("/{eventId:guid}", async (Guid organizationId, Guid eventId, HttpContext context,
            IAuditTrail audit, CancellationToken ct) =>
        {
            var value = await audit.ReadAsync(Identity(context), organizationId, eventId, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        });
    }

    private static bool TryQuery(HttpContext context, out int pageSize, out long? after,
        out string? action, out Guid? branchId)
    {
        pageSize = 50; after = null; action = null; branchId = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "afterSequence"
            and not "action" and not "branchId")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var size)
            && (size.Count != 1 || !int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize)
                || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("afterSequence", out var cursor))
        {
            if (cursor.Count != 1 || !long.TryParse(cursor[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 0) return false;
            after = value;
        }
        if (context.Request.Query.TryGetValue("action", out var actions))
        {
            if (actions.Count != 1 || string.IsNullOrWhiteSpace(actions[0]) || actions[0]!.Length > 180) return false;
            action = actions[0];
        }
        if (context.Request.Query.TryGetValue("branchId", out var branches))
        {
            if (branches.Count != 1 || !Guid.TryParseExact(branches[0], "D", out var value) || value == Guid.Empty) return false;
            branchId = value;
        }
        return true;
    }

    private static bool TryVerify(HttpContext context, out long? from, out int limit)
    {
        from = null; limit = 500;
        if (context.Request.Query.Keys.Any(key => key is not "fromSequence" and not "limit")) return false;
        if (context.Request.Query.TryGetValue("fromSequence", out var starts))
        {
            if (starts.Count != 1 || !long.TryParse(starts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 1) return false;
            from = value;
        }
        if (context.Request.Query.TryGetValue("limit", out var limits)
            && (limits.Count != 1 || !int.TryParse(limits[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                || limit is < 1 or > 1000)) return false;
        return true;
    }

    private static AuditIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The audit request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_audit_request" });
}

