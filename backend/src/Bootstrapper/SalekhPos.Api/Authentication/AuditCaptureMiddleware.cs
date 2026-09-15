using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Audit.Application.AuditTrail;
using SalekhPos.Audit.Domain.AuditEvents;

namespace SalekhPos.Api.Authentication;

public sealed class AuditCaptureMiddleware(RequestDelegate next, ILogger<AuditCaptureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditTrail audit)
    {
        if (!IsMutation(context.Request.Method)
            || !Guid.TryParse(context.Request.RouteValues["organizationId"]?.ToString(), out var organizationId)
            || organizationId == Guid.Empty)
        {
            await next(context); return;
        }
        var identity = await ResolveIdentity(context);
        if (identity is null) { await next(context); return; }
        var descriptor = Describe(context);
        var attempted = NewDraft(context, organizationId, descriptor, AuditOutcome.Attempted);
        await audit.AppendAsync(identity, new(attempted), context.RequestAborted);
        await next(context);
        var outcome = context.Response.StatusCode < 400 ? AuditOutcome.Succeeded : AuditOutcome.Failed;
        try
        {
            var completed = NewDraft(context, organizationId, descriptor, outcome);
            await audit.AppendAsync(identity, new(completed), context.RequestAborted);
        }
        catch (Exception exception) when (exception is AuditUnavailableException or AuditConflictException)
        {
            AuditCompletionFailed(logger, context.TraceIdentifier, exception.GetType().Name);
        }
    }

    private static bool IsMutation(string method) => method is "POST" or "PUT" or "PATCH" or "DELETE";

    private static async Task<AuditIdentity?> ResolveIdentity(HttpContext context)
    {
        var principal = context.User;
        if (context.Request.Path.StartsWithSegments("/bff"))
        {
            var web = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
            if (web.Succeeded && web.Principal is not null) principal = web.Principal;
        }
        var issuers = principal.FindAll("iss").ToArray(); var subjects = principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try { return new AuditIdentity(issuers[0].Value, subjects[0].Value); }
        catch (ArgumentException) { return null; }
    }

    private static (string Action, string TargetType, Guid? TargetId, Guid? BranchId, Guid? DeviceId) Describe(HttpContext context)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? "/";
        var action = $"{context.Request.Method} {pattern}";
        if (action.Length > 180) action = action[..180];
        var targetId = RouteGuid(context, "saleId", "returnId", "orderId", "transferId", "promotionId",
            "accountId", "customerId", "supplierId", "employeeId", "productId", "registerId", "shiftId", "messageId");
        var branchId = RouteGuid(context, "branchId"); var deviceId = RouteGuid(context, "deviceId");
        return (action, TargetType(pattern), targetId, branchId, deviceId);
    }

    private static AuditEventDraft NewDraft(HttpContext context, Guid organizationId,
        (string Action, string TargetType, Guid? TargetId, Guid? BranchId, Guid? DeviceId) descriptor,
        AuditOutcome outcome)
    {
        var sourceIp = context.Connection.RemoteIpAddress?.ToString();
        var correlation = Correlation(context);
        return new(organizationId, Guid.NewGuid(), Guid.NewGuid(), descriptor.Action, descriptor.TargetType,
            descriptor.TargetId, descriptor.BranchId, descriptor.DeviceId, sourceIp, correlation,
            context.TraceIdentifier, outcome, null);
    }

    private static string Correlation(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var values) && values.Count == 1)
        {
            var value = values[0];
            if (!string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= 128
                && !value.Any(char.IsControl)) return value;
        }
        return context.TraceIdentifier.Length <= 128 ? context.TraceIdentifier : context.TraceIdentifier[..128];
    }

    private static Guid? RouteGuid(HttpContext context, params string[] names)
    {
        foreach (var name in names)
            if (Guid.TryParse(context.Request.RouteValues[name]?.ToString(), out var value) && value != Guid.Empty) return value;
        return null;
    }

    private static string TargetType(string pattern)
    {
        if (pattern.Contains("stock-transfers", StringComparison.Ordinal)) return "stock_transfer";
        if (pattern.Contains("promotions", StringComparison.Ordinal)) return "promotion";
        if (pattern.Contains("loyalty", StringComparison.Ordinal)) return "loyalty_account";
        if (pattern.Contains("purchase-orders", StringComparison.Ordinal)) return "purchase_order";
        if (pattern.Contains("customers", StringComparison.Ordinal)) return "customer";
        if (pattern.Contains("suppliers", StringComparison.Ordinal)) return "supplier";
        if (pattern.Contains("employees", StringComparison.Ordinal)) return "employee";
        if (pattern.Contains("inventory", StringComparison.Ordinal)) return "inventory";
        if (pattern.Contains("prices", StringComparison.Ordinal) || pattern.Contains("pricing", StringComparison.Ordinal)) return "price";
        if (pattern.Contains("shifts", StringComparison.Ordinal)) return "shift";
        if (pattern.Contains("sales", StringComparison.Ordinal)) return "sale";
        if (pattern.Contains("returns", StringComparison.Ordinal)) return "return";
        return "http_mutation";
    }

    private static void AuditCompletionFailed(ILogger logger, string traceId, string failureClass) =>
        logger.LogCritical("Audit completion persistence failed for trace {TraceId}; class {FailureClass}",
            traceId, failureClass);
}

public static class AuditCaptureMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditCapture(this IApplicationBuilder app) =>
        app.UseMiddleware<AuditCaptureMiddleware>();
}
