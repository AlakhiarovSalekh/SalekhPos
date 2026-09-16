namespace SalekhPos.Localization.Contracts.Settings;

public sealed record UpdateLocalizationSettingsRequest(
    string CountryCode,
    string DefaultLocale,
    string DefaultCurrency,
    string TimeZone,
    IReadOnlyList<string> SupportedLocales,
    int FirstDayOfWeek,
    long? ExpectedVersion);

public sealed record LocalizationSettingsResponse(
    string CountryCode,
    string DefaultLocale,
    string DefaultCurrency,
    string TimeZone,
    IReadOnlyList<string> SupportedLocales,
    int FirstDayOfWeek,
    long Version,
    DateTimeOffset UpdatedAt);
