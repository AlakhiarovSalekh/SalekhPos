using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Notifications.Contracts.Notifications;
using SalekhPos.Taxation.Application.TaxConfiguration;
using SalekhPos.Taxation.Contracts.TaxConfiguration;
using SalekhPos.Localization.Application.Settings;
using SalekhPos.Localization.Contracts.Settings;

namespace SalekhPos.Api.Authentication;

public static class WebGlobalConfigurationEndpoints
{
    private sealed record BrowserNotificationRequest(Guid? BranchId, string Title, string Body, string Severity);

    public static void MapWebGlobalConfigurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1")
            .AllowAnonymous()
            .RequireRateLimiting("business");

        MapNotifications(group);
        MapTaxation(group);
        MapLocalization(group);
    }

    private static void MapNotifications(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/notifications", ListNotifications);
        group.MapPost("/organizations/{organizationId:guid}/notifications", CreateNotification);
        group.MapPost("/organizations/{organizationId:guid}/notifications/{notificationId:guid}/read", MarkRead);
        group.MapGet("/organizations/{organizationId:guid}/notification-deliveries", ListNotificationDeliveries);
        group.MapPost("/organizations/{organizationId:guid}/notification-deliveries/{deliveryId:guid}/retry", RetryNotificationDelivery);
        group.MapGet("/organizations/{organizationId:guid}/notification-preferences", GetPreferences);
        group.MapPut("/organizations/{organizationId:guid}/notification-preferences", UpdatePreferences);
    }

    private static async Task<IResult> ListNotifications(
        Guid organizationId,
        HttpContext context,
        WebAuthenticationState state,
        INotificationCenter notifications,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (organizationId == Guid.Empty) return Invalid("invalid_notification_query");

        if (!TryNotificationQuery(context, out var unreadOnly, out var pageSize, out var after))
            return Invalid("invalid_notification_query");

        return Results.Ok(await notifications.ListMineAsync(
            identity, organizationId, pageSize, after, unreadOnly, cancellationToken));
    }

    private static async Task<IResult> CreateNotification(
        Guid organizationId, BrowserNotificationRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery,
        INotificationCenter notifications, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
            return Invalid("invalid_notification_request");

        try
        {
            var result = await notifications.CreateAsync(identity,
                new(organizationId, Guid.NewGuid(), operationId, request.BranchId, identity.Subject,
                    request.Title, request.Body, request.Severity),
                cancellationToken);
            return result.Created ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/notifications/{result.Notification.Id:D}", result.Notification)
                : Results.Ok(result.Notification);
        }
        catch (ArgumentException) { return Invalid("invalid_notification_request"); }
    }

    private static async Task<IResult> MarkRead(
        Guid organizationId, Guid notificationId, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery,
        INotificationCenter notifications, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (notificationId == Guid.Empty || !await ValidMutation(context, state, antiforgery))
            return Invalid("invalid_notification_request");
        return Results.Ok(await notifications.MarkReadAsync(
            identity, organizationId, notificationId, cancellationToken));
    }


    private static async Task<IResult> ListNotificationDeliveries(
        Guid organizationId,
        HttpContext context,
        WebAuthenticationState state,
        INotificationDeliveryStore deliveries,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (organizationId == Guid.Empty
            || !TryDeliveryQuery(context, out var pageSize, out var after, out var status, out var channel))
        {
            return Invalid("invalid_notification_delivery_query");
        }

        try
        {
            return Results.Ok(await deliveries.ListAsync(
                identity,
                organizationId,
                pageSize,
                after,
                status,
                channel,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Invalid("invalid_notification_delivery_query");
        }
    }

    private static async Task<IResult> RetryNotificationDelivery(
        Guid organizationId,
        Guid deliveryId,
        RetryNotificationDeliveryRequest request,
        HttpContext context,
        WebAuthenticationState state,
        IAntiforgery antiforgery,
        INotificationDeliveryStore deliveries,
        CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (organizationId == Guid.Empty
            || deliveryId == Guid.Empty
            || !await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
        {
            return Invalid("invalid_notification_delivery_retry");
        }

        try
        {
            return Results.Ok(await deliveries.RetryDeadLetterAsync(
                identity,
                organizationId,
                deliveryId,
                operationId,
                request.Reason,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Invalid("invalid_notification_delivery_retry");
        }
    }

    private static async Task<IResult> GetPreferences(
        Guid organizationId, HttpContext context, WebAuthenticationState state,
        INotificationCenter notifications, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        return Results.Ok(await notifications.ReadPreferencesAsync(
            identity, organizationId, cancellationToken));
    }

    private static async Task<IResult> UpdatePreferences(
        Guid organizationId, UpdateNotificationPreferencesRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery,
        INotificationCenter notifications, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!await ValidMutation(context, state, antiforgery))
            return Invalid("invalid_notification_preferences");
        return Results.Ok(await notifications.UpdatePreferencesAsync(
            identity, organizationId, request, cancellationToken));
    }

    private static void MapTaxation(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/tax-profiles", ListTaxProfiles);
        group.MapPost("/organizations/{organizationId:guid}/tax-profiles", CreateTaxProfile);
        group.MapGet("/organizations/{organizationId:guid}/tax-profiles/{profileId:guid}/rates", ListTaxRates);
        group.MapPost("/organizations/{organizationId:guid}/tax-profiles/{profileId:guid}/rates", CreateTaxRate);
        group.MapPost("/organizations/{organizationId:guid}/tax-profiles/calculate", CalculateTax);
    }

    private static async Task<IResult> ListTaxProfiles(
        Guid organizationId, HttpContext context, WebAuthenticationState state,
        ITaxConfiguration tax, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!WebBusinessEndpoints.TryListQuery(context, out var pageSize, out var after))
            return Invalid("invalid_tax_query");
        return Results.Ok(await tax.ListProfilesAsync(
            AsTaxIdentity(identity), organizationId, pageSize, after, cancellationToken));
    }

    private static async Task<IResult> CreateTaxProfile(
        Guid organizationId, CreateTaxProfileRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery,
        ITaxConfiguration tax, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
            return Invalid("invalid_tax_request");
        try
        {
            var result = await tax.CreateProfileAsync(AsTaxIdentity(identity),
                new(organizationId, Guid.NewGuid(), operationId, request.Code,
                    request.Name, request.CountryCode, request.PricesIncludeTax), cancellationToken);
            return result.Created
                ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/tax-profiles/{result.Profile.Id:D}", result.Profile)
                : Results.Ok(result.Profile);
        }
        catch (ArgumentException) { return Invalid("invalid_tax_request"); }
    }

    private static async Task<IResult> ListTaxRates(
        Guid organizationId, Guid profileId, HttpContext context,
        WebAuthenticationState state, ITaxConfiguration tax, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (profileId == Guid.Empty) return Invalid("invalid_tax_query");
        return Results.Ok(await tax.ListRatesAsync(
            AsTaxIdentity(identity), organizationId, profileId, cancellationToken));
    }

    private static async Task<IResult> CreateTaxRate(
        Guid organizationId, Guid profileId, CreateTaxRateRequest request, HttpContext context,
        WebAuthenticationState state, IAntiforgery antiforgery,
        ITaxConfiguration tax, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (profileId == Guid.Empty || !await ValidMutation(context, state, antiforgery)
            || !TryOperationId(context, out var operationId))
            return Invalid("invalid_tax_request");
        try
        {
            var result = await tax.CreateRateAsync(AsTaxIdentity(identity),
                new(organizationId, Guid.NewGuid(), operationId, profileId, request.BranchId,
                    request.CategoryCode, request.RatePercent, request.EffectiveFrom, request.EffectiveUntil),
                cancellationToken);
            return result.Created
                ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/tax-profiles/{profileId:D}/rates/{result.Rate.Id:D}", result.Rate)
                : Results.Ok(result.Rate);
        }
        catch (ArgumentException) { return Invalid("invalid_tax_request"); }
    }

    private static async Task<IResult> CalculateTax(
        Guid organizationId, CalculateTaxRequest request, HttpContext context,
        WebAuthenticationState state, ITaxConfiguration tax, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        try
        {
            return Results.Ok(await tax.CalculateAsync(
                AsTaxIdentity(identity), organizationId, request, cancellationToken));
        }
        catch (ArgumentException) { return Invalid("invalid_tax_request"); }
    }

    private static async Task<NotificationIdentity?> Identity(
        HttpContext context, WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        try { return new NotificationIdentity(issuers[0].Value, subjects[0].Value); }
        catch (ArgumentException) { return null; }
    }

    private static void MapLocalization(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/localization", ReadLocalization);
        group.MapPut("/organizations/{organizationId:guid}/localization", UpdateLocalization);
    }

    private static async Task<IResult> ReadLocalization(Guid organizationId, HttpContext context,
        WebAuthenticationState state, ILocalizationSettings settings, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        return Results.Ok(await settings.ReadAsync(AsLocalizationIdentity(identity), organizationId, cancellationToken));
    }

    private static async Task<IResult> UpdateLocalization(Guid organizationId,
        UpdateLocalizationSettingsRequest request, HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery, ILocalizationSettings settings, CancellationToken cancellationToken)
    {
        var identity = await Identity(context, state);
        if (identity is null) return Unauthenticated(state);
        if (!await ValidMutation(context, state, antiforgery) || !TryOperationId(context, out var operationId))
            return Invalid("invalid_localization_request");
        try
        {
            return Results.Ok(await settings.UpdateAsync(AsLocalizationIdentity(identity),
                new(organizationId, operationId, request.CountryCode, request.DefaultLocale,
                    request.DefaultCurrency, request.TimeZone, request.SupportedLocales,
                    request.FirstDayOfWeek, request.ExpectedVersion), cancellationToken));
        }
        catch (ArgumentException) { return Invalid("invalid_localization_request"); }
    }

    private static TaxIdentity AsTaxIdentity(NotificationIdentity identity) =>
        new(identity.Issuer, identity.Subject);

    private static LocalizationIdentity AsLocalizationIdentity(NotificationIdentity identity) =>
        new(identity.Issuer, identity.Subject);

    private static Task<bool> ValidMutation(
        HttpContext context, WebAuthenticationState state, IAntiforgery antiforgery) =>
        state.Settings is null
            ? Task.FromResult(false)
            : WebAuthentication.ValidateMutation(context, antiforgery, state.Settings);

    private static bool TryOperationId(HttpContext context, out Guid operationId) =>
        Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out operationId)
        && operationId != Guid.Empty;


    private static bool TryDeliveryQuery(
        HttpContext context,
        out int pageSize,
        out Guid? after,
        out string? status,
        out string? channel)
    {
        pageSize = 25;
        after = null;
        status = null;
        channel = null;
        if (context.Request.Query.Keys.Any(key =>
                key is not "pageSize" and not "after" and not "status" and not "channel"))
        {
            return false;
        }

        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], out pageSize)
                || pageSize is < 1 or > 100))
        {
            return false;
        }

        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty)
            {
                return false;
            }
            after = cursor;
        }

        if (context.Request.Query.TryGetValue("status", out var statuses))
        {
            if (statuses.Count != 1) return false;
            status = statuses[0];
        }

        if (context.Request.Query.TryGetValue("channel", out var channels))
        {
            if (channels.Count != 1) return false;
            channel = channels[0];
        }

        return true;
    }

    private static bool TryNotificationQuery(
        HttpContext context, out bool unreadOnly, out int pageSize, out Guid? after)
    {
        unreadOnly = false;
        pageSize = 25;
        after = null;
        if (context.Request.Query.Keys.Any(key =>
                key is not "unreadOnly" and not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("unreadOnly", out var unreadValues)
            && (unreadValues.Count != 1 || !bool.TryParse(unreadValues[0], out unreadOnly))) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], out pageSize)
                || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }

    private static IResult Unauthenticated(WebAuthenticationState state) =>
        state.Settings is null
            ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "web_authentication_unavailable"
                })
            : Results.Unauthorized();

    private static IResult Invalid(string code) =>
        Results.Problem(statusCode: 400,
            title: "The global configuration request is invalid",
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
