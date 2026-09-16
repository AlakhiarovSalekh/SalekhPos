using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Localization.Application.Settings;
using SalekhPos.Localization.Contracts.Settings;

namespace SalekhPos.Localization.Api.Settings;

public static class LocalizationEndpoints
{
    public static void MapLocalizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/localization")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", Read);
        group.MapPut("", Update);
    }

    private static LocalizationIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static async Task<IResult> Read(Guid organizationId, HttpContext context,
        ILocalizationSettings settings, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) return Invalid();
        return Results.Ok(await settings.ReadAsync(Identity(context), organizationId, cancellationToken));
    }
    private static async Task<IResult> Update(Guid organizationId,
        UpdateLocalizationSettingsRequest request, HttpContext context, IAntiforgery antiforgery,
        ILocalizationSettings settings, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || !await Mutation(context, antiforgery)
            || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
            || operationId == Guid.Empty) return Invalid();
        try
        {
            var result = await settings.UpdateAsync(Identity(context),
                new(organizationId, operationId, request.CountryCode, request.DefaultLocale,
                    request.DefaultCurrency, request.TimeZone, request.SupportedLocales,
                    request.FirstDayOfWeek, request.ExpectedVersion), cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException) { return Invalid(); }
    }

    private static async Task<bool> Mutation(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Headers.ContainsKey("Authorization")) return true;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The localization request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_localization_request" });
}
