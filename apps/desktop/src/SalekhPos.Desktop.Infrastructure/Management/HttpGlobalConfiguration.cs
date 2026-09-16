using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpGlobalConfiguration(HttpClient client) : IGlobalConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UuidPage<NotificationSummary>> ListNotificationsAsync(Guid organizationId,
        int pageSize, Guid? after, bool unreadOnly, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/notifications?pageSize={pageSize}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await Get<UuidPage<NotificationSummary>>(path, cancellationToken);
        if (result.Items.Count > pageSize) throw new InvalidOperationException("Notification page is too large.");
        foreach (var item in result.Items) ValidateNotification(item);
        return result;
    }

    public async Task<NotificationSummary> MarkNotificationReadAsync(Guid organizationId,
        Guid notificationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        if (notificationId == Guid.Empty) throw new ArgumentException("Notification is required.");
        var result = await Post<NotificationSummary>($"api/v1/organizations/{organizationId:D}/notifications/{notificationId:D}/read",
            new { }, null, cancellationToken);
        ValidateNotification(result);
        if (result.Id != notificationId || !result.IsRead) throw new InvalidOperationException("Notification read response is invalid.");
        return result;
    }
    public async Task<NotificationPreferencesSummary> ReadNotificationPreferencesAsync(Guid organizationId,
        CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        var result = await Get<NotificationPreferencesSummary>(
            $"api/v1/organizations/{organizationId:D}/notifications/preferences", cancellationToken);
        ValidatePreferences(result); return result;
    }

    public async Task<NotificationPreferencesSummary> UpdateNotificationPreferencesAsync(Guid organizationId,
        bool inAppEnabled, bool emailEnabled, bool pushEnabled, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        var result = await Put<NotificationPreferencesSummary>(
            $"api/v1/organizations/{organizationId:D}/notifications/preferences",
            new { InAppEnabled = inAppEnabled, EmailEnabled = emailEnabled, PushEnabled = pushEnabled }, cancellationToken);
        ValidatePreferences(result); return result;
    }

    public async Task<UuidPage<TaxProfileSummary>> ListTaxProfilesAsync(Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/tax-profiles?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await Get<UuidPage<TaxProfileSummary>>(path, cancellationToken);
        if (result.Items.Count > pageSize) throw new InvalidOperationException("Tax profile page is too large.");
        foreach (var item in result.Items) ValidateProfile(item);
        return result;
    }
    public async Task<TaxProfileSummary> CreateTaxProfileAsync(Guid organizationId,
        CreateTaxProfileInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId); ValidateProfileInput(input);
        var result = await Post<TaxProfileSummary>($"api/v1/organizations/{organizationId:D}/tax-profiles",
            input, operationId, cancellationToken);
        ValidateProfile(result); return result;
    }

    public async Task<IReadOnlyList<TaxRateSummary>> ListTaxRatesAsync(Guid organizationId,
        Guid profileId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        if (profileId == Guid.Empty) throw new ArgumentException("Tax profile is required.");
        var result = await Get<List<TaxRateSummary>>(
            $"api/v1/organizations/{organizationId:D}/tax-profiles/{profileId:D}/rates", cancellationToken);
        if (result.Count > 500) throw new InvalidOperationException("Too many tax rates were returned.");
        foreach (var item in result) ValidateRate(item, profileId);
        return result;
    }

    public async Task<TaxRateSummary> CreateTaxRateAsync(Guid organizationId, Guid profileId,
        CreateTaxRateInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId);
        if (profileId == Guid.Empty) throw new ArgumentException("Tax profile is required.");
        ValidateRateInput(input);
        var result = await Post<TaxRateSummary>($"api/v1/organizations/{organizationId:D}/tax-profiles/{profileId:D}/rates",
            input, operationId, cancellationToken);
        ValidateRate(result, profileId); return result;
    }
    public async Task<TaxCalculationSummary> CalculateTaxAsync(Guid organizationId, Guid profileId,
        Guid branchId, string categoryCode, decimal amount, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        if (profileId == Guid.Empty || branchId == Guid.Empty || amount < 0 || InvalidAmount(amount)
            || at.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(categoryCode) || categoryCode.Length > 40)
            throw new ArgumentException("Tax calculation input is invalid.");
        var result = await Post<TaxCalculationSummary>($"api/v1/organizations/{organizationId:D}/tax-profiles/calculate",
            new { ProfileId = profileId, BranchId = branchId, CategoryCode = categoryCode, Amount = amount, At = at },
            null, cancellationToken);
        if (result.ProfileId != profileId || !result.CategoryCode.Equals(categoryCode.Trim(), StringComparison.InvariantCultureIgnoreCase)
            || result.NetAmount < 0 || result.TaxAmount < 0 || result.GrossAmount < 0
            || result.NetAmount + result.TaxAmount != result.GrossAmount)
            throw new InvalidOperationException("Tax calculation response is invalid.");
        return result;
    }

    public async Task<LocalizationSettingsSummary> ReadLocalizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        var result = await Get<LocalizationSettingsSummary>($"api/v1/organizations/{organizationId:D}/localization", cancellationToken);
        ValidateLocalization(result); return result;
    }

    public async Task<LocalizationSettingsSummary> UpdateLocalizationAsync(Guid organizationId, UpdateLocalizationInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId); ValidateLocalizationInput(input);
        var result = await PutWithIdempotency<LocalizationSettingsSummary>($"api/v1/organizations/{organizationId:D}/localization", input, operationId, cancellationToken);
        ValidateLocalization(result); return result;
    }

    private async Task<T> Get<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }
    private async Task<T> Post<T>(string path, object body, Guid? operationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        { Content = JsonContent.Create(body, options: JsonOptions) };
        if (operationId.HasValue)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.Value.ToString("D"));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }

    private async Task<T> Put<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await client.PutAsJsonAsync(path, body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }

    private async Task<T> PutWithIdempotency<T>(string path, object body, Guid operationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body, options: JsonOptions) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("The server response is empty.");
    }

    private static void ValidateLocalization(LocalizationSettingsSummary value)
    {
        if (value.CountryCode.Length != 2 || value.CountryCode.Any(c => c < 'A' || c > 'Z') || value.DefaultCurrency.Length != 3
            || value.DefaultCurrency.Any(c => c < 'A' || c > 'Z') || string.IsNullOrWhiteSpace(value.DefaultLocale) || string.IsNullOrWhiteSpace(value.TimeZone)
            || value.SupportedLocales.Count is < 1 or > 20 || !value.SupportedLocales.Contains(value.DefaultLocale) || value.FirstDayOfWeek is < 1 or > 7
            || value.Version < 1 || value.UpdatedAt.Offset != TimeSpan.Zero) throw new InvalidOperationException("Localization response is invalid.");
    }

    private static void ValidateLocalizationInput(UpdateLocalizationInput value)
    {
        if (value.CountryCode.Length != 2 || value.DefaultCurrency.Length != 3 || string.IsNullOrWhiteSpace(value.DefaultLocale)
            || string.IsNullOrWhiteSpace(value.TimeZone) || value.SupportedLocales.Count is < 1 or > 20
            || !value.SupportedLocales.Contains(value.DefaultLocale) || value.FirstDayOfWeek is < 1 or > 7) throw new ArgumentException("Localization input is invalid.");
    }

    private static void ValidateNotification(NotificationSummary value)
    {
        if (value.Id == Guid.Empty || string.IsNullOrWhiteSpace(value.Title) || value.Title.Length > 160
            || string.IsNullOrWhiteSpace(value.Body) || value.Body.Length > 2000
            || value.Severity is not ("info" or "warning" or "critical")
            || value.CreatedAt.Offset != TimeSpan.Zero || value.IsRead != value.ReadAt.HasValue)
            throw new InvalidOperationException("Notification response is invalid.");
    }
    private static void ValidatePreferences(NotificationPreferencesSummary value)
    {
        if (value.UpdatedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Notification preferences response is invalid.");
    }

    private static void ValidateProfile(TaxProfileSummary value)
    {
        if (value.Id == Guid.Empty || string.IsNullOrWhiteSpace(value.Code) || value.Code.Length > 40
            || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 120
            || value.CountryCode.Length != 2 || value.CountryCode.Any(c => c < 'A' || c > 'Z')
            || value.Version < 1 || value.CreatedAt.Offset != TimeSpan.Zero || value.UpdatedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Tax profile response is invalid.");
    }

    private static void ValidateRate(TaxRateSummary value, Guid profileId)
    {
        if (value.Id == Guid.Empty || value.ProfileId != profileId || string.IsNullOrWhiteSpace(value.CategoryCode)
            || value.CategoryCode.Length > 40 || value.RatePercent is < 0 or > 100 || InvalidAmount(value.RatePercent)
            || value.EffectiveFrom.Offset != TimeSpan.Zero || value.EffectiveUntil.HasValue
            && (value.EffectiveUntil.Value.Offset != TimeSpan.Zero || value.EffectiveUntil <= value.EffectiveFrom)
            || value.Version < 1)
            throw new InvalidOperationException("Tax rate response is invalid.");
    }
    private static void ValidateProfileInput(CreateTaxProfileInput value)
    {
        if (string.IsNullOrWhiteSpace(value.Code) || value.Code.Length > 40
            || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 120
            || value.CountryCode.Length != 2 || value.CountryCode.Any(c => c < 'A' || c > 'Z'))
            throw new ArgumentException("Tax profile input is invalid.");
    }

    private static void ValidateRateInput(CreateTaxRateInput value)
    {
        if (string.IsNullOrWhiteSpace(value.CategoryCode) || value.CategoryCode.Length > 40
            || value.RatePercent is < 0 or > 100 || InvalidAmount(value.RatePercent)
            || value.EffectiveFrom.Offset != TimeSpan.Zero || value.EffectiveUntil.HasValue
            && (value.EffectiveUntil.Value.Offset != TimeSpan.Zero || value.EffectiveUntil <= value.EffectiveFrom))
            throw new ArgumentException("Tax rate input is invalid.");
    }

    private static void ValidateOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
    }
    private static void ValidatePage(int pageSize)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
    private static void ValidateOperation(Guid operationId)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
    }
    private static bool InvalidAmount(decimal value) => decimal.Round(value, 6) != value;
}
