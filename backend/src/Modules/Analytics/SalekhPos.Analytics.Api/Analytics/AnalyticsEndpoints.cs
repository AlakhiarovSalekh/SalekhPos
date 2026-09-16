using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Analytics.Application.Analytics;
using SalekhPos.Analytics.Domain.Analytics;

namespace SalekhPos.Analytics.Api.Analytics;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var branches = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/analytics")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        branches.MapGet("/overview", Overview);
        branches.MapGet("/sales-trend", SalesTrend);
        app.MapGet("/api/v1/organizations/{organizationId:guid}/analytics/stores", StoreComparison)
            .RequireAuthorization("business-api").RequireRateLimiting("business");
    }

    private static async Task<IResult> Overview(Guid organizationId, Guid branchId, HttpContext context,
        IAnalyticsReader analytics, CancellationToken ct)
    {
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.ReadOverviewAsync(Identity(context), organizationId, branchId, window, ct));
    }

    private static async Task<IResult> SalesTrend(Guid organizationId, Guid branchId, HttpContext context,
        IAnalyticsReader analytics, CancellationToken ct)
    {
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.ReadSalesTrendAsync(Identity(context), organizationId, branchId, window, ct));
    }

    private static async Task<IResult> StoreComparison(Guid organizationId, HttpContext context,
        IAnalyticsReader analytics, CancellationToken ct)
    {
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.CompareStoresAsync(Identity(context), organizationId, window, ct));
    }

    private static AnalyticsIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static bool TryWindow(HttpContext context, out AnalyticsWindow window)
    {
        window = null!;
        if (context.Request.Query.Keys.Any(key => key is not "from" and not "to")) return false;
        if (!context.Request.Query.TryGetValue("from", out var fromValues) || fromValues.Count != 1
            || !DateTimeOffset.TryParse(fromValues[0], out var from) || from.Offset != TimeSpan.Zero) return false;
        if (!context.Request.Query.TryGetValue("to", out var toValues) || toValues.Count != 1
            || !DateTimeOffset.TryParse(toValues[0], out var to) || to.Offset != TimeSpan.Zero) return false;
        try { window = new(from, to); return true; }
        catch (ArgumentException) { return false; }
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The analytics request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_analytics_request" });
}
