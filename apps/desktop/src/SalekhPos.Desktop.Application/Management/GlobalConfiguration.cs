namespace SalekhPos.Desktop.Application.Management;

public sealed record NotificationSummary(Guid Id, Guid? BranchId, string RecipientSubject,
    string Title, string Body, string Severity, bool IsRead,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPreferencesSummary(bool InAppEnabled, bool EmailEnabled,
    bool PushEnabled, DateTimeOffset UpdatedAt);
public sealed record TaxProfileSummary(Guid Id, string Code, string Name, string CountryCode,
    bool PricesIncludeTax, bool IsActive, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record TaxRateSummary(Guid Id, Guid ProfileId, Guid? BranchId,
    string CategoryCode, decimal RatePercent, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil, bool IsActive, long Version);
public sealed record TaxCalculationSummary(Guid ProfileId, Guid? RateId, string CategoryCode,
    decimal RatePercent, decimal NetAmount, decimal TaxAmount, decimal GrossAmount,
    bool PricesIncludeTax);

public sealed record CreateTaxProfileInput(string Code, string Name, string CountryCode,
    bool PricesIncludeTax);
public sealed record CreateTaxRateInput(Guid? BranchId, string CategoryCode,
    decimal RatePercent, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil);
public interface IGlobalConfiguration
{
    Task<UuidPage<NotificationSummary>> ListNotificationsAsync(Guid organizationId,
        int pageSize, Guid? after, bool unreadOnly, CancellationToken cancellationToken);
    Task<NotificationSummary> MarkNotificationReadAsync(Guid organizationId,
        Guid notificationId, CancellationToken cancellationToken);
    Task<NotificationPreferencesSummary> ReadNotificationPreferencesAsync(Guid organizationId,
        CancellationToken cancellationToken);
    Task<NotificationPreferencesSummary> UpdateNotificationPreferencesAsync(Guid organizationId,
        bool inAppEnabled, bool emailEnabled, bool pushEnabled, CancellationToken cancellationToken);
    Task<UuidPage<TaxProfileSummary>> ListTaxProfilesAsync(Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<TaxProfileSummary> CreateTaxProfileAsync(Guid organizationId,
        CreateTaxProfileInput input, Guid operationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaxRateSummary>> ListTaxRatesAsync(Guid organizationId,
        Guid profileId, CancellationToken cancellationToken);
    Task<TaxRateSummary> CreateTaxRateAsync(Guid organizationId, Guid profileId,
        CreateTaxRateInput input, Guid operationId, CancellationToken cancellationToken);
    Task<TaxCalculationSummary> CalculateTaxAsync(Guid organizationId, Guid profileId,
        Guid branchId, string categoryCode, decimal amount, DateTimeOffset at,
        CancellationToken cancellationToken);
    Task<LocalizationSettingsSummary> ReadLocalizationAsync(Guid organizationId,
        CancellationToken cancellationToken);
    Task<LocalizationSettingsSummary> UpdateLocalizationAsync(Guid organizationId,
        UpdateLocalizationInput input, Guid operationId, CancellationToken cancellationToken);
}

public sealed record LocalizationSettingsSummary(string CountryCode, string DefaultLocale,
    string DefaultCurrency, string TimeZone, IReadOnlyList<string> SupportedLocales,
    int FirstDayOfWeek, long Version, DateTimeOffset UpdatedAt);
public sealed record UpdateLocalizationInput(string CountryCode, string DefaultLocale,
    string DefaultCurrency, string TimeZone, IReadOnlyList<string> SupportedLocales,
    int FirstDayOfWeek, long? ExpectedVersion);
