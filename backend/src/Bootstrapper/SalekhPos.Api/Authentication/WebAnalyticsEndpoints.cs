using Microsoft.AspNetCore.Authentication;
using SalekhPos.Analytics.Application.Analytics;
using SalekhPos.Analytics.Domain.Analytics;

namespace SalekhPos.Api.Authentication;

public static class WebAnalyticsEndpoints
{
    public static void MapWebAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1")
            .AllowAnonymous()
            .RequireRateLimiting("business");

        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/analytics/overview", Overview);
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/analytics/sales-trend", Trend);
        group.MapGet("/organizations/{organizationId:guid}/analytics/stores", Stores);
    }
    private static async Task<IResult> Overview(Guid organizationId, Guid branchId,
        HttpContext context, WebAuthenticationState state, IAnalyticsReader analytics,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.ReadOverviewAsync(identity, organizationId,
            branchId, window, cancellationToken));
    }

    private static async Task<IResult> Trend(Guid organizationId, Guid branchId,
        HttpContext context, WebAuthenticationState state, IAnalyticsReader analytics,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.ReadSalesTrendAsync(identity, organizationId,
            branchId, window, cancellationToken));
    }
    private static async Task<IResult> Stores(Guid organizationId,
        HttpContext context, WebAuthenticationState state, IAnalyticsReader analytics,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!TryWindow(context, out var window)) return Invalid();
        return Results.Ok(await analytics.CompareStoresAsync(identity, organizationId,
            window, cancellationToken));
    }

    private static async Task<AnalyticsIdentity?> Identity(
        HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try { var value = new AnalyticsIdentity(issuers[0].Value, subjects[0].Value); value.Validate(); return value; }
        catch (ArgumentException) { return null; }
    }
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

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The analytics request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_analytics_request" });
}
