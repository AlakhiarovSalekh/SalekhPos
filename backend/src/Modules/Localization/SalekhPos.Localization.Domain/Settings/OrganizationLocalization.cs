using System.Text.RegularExpressions;

namespace SalekhPos.Localization.Domain.Settings;

public sealed partial class OrganizationLocalization
{
    public OrganizationLocalization(Guid organizationId, string countryCode, string defaultLocale,
        string defaultCurrency, string timeZone, IReadOnlyList<string> supportedLocales, int firstDayOfWeek)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        OrganizationId = organizationId;
        CountryCode = NormalizeCountry(countryCode);
        DefaultLocale = NormalizeLocale(defaultLocale);
        DefaultCurrency = NormalizeCurrency(defaultCurrency);
        TimeZone = NormalizeTimeZone(timeZone);
        if (firstDayOfWeek is < 1 or > 7) throw new ArgumentException("First day of week is invalid.");
        FirstDayOfWeek = firstDayOfWeek;
        if (supportedLocales is null || supportedLocales.Count is < 1 or > 20)
            throw new ArgumentException("Supported locales are invalid.");
        var locales = supportedLocales.Select(NormalizeLocale).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (locales.Length != supportedLocales.Count || !locales.Contains(DefaultLocale, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Supported locales must be unique and include the default locale.");
        SupportedLocales = locales;
    }

    public Guid OrganizationId { get; }
    public string CountryCode { get; }
    public string DefaultLocale { get; }
    public string DefaultCurrency { get; }
    public string TimeZone { get; }
    public IReadOnlyList<string> SupportedLocales { get; }
    public int FirstDayOfWeek { get; }
    private static string NormalizeCountry(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? "";
        if (!CountryCodePattern().IsMatch(normalized)) throw new ArgumentException("Country code is invalid.");
        return normalized;
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? "";
        if (!CurrencyPattern().IsMatch(normalized)) throw new ArgumentException("Currency is invalid.");
        return normalized;
    }

    private static string NormalizeLocale(string value)
    {
        var normalized = value?.Trim() ?? "";
        if (!LocalePattern().IsMatch(normalized)) throw new ArgumentException("Locale is invalid.");
        return normalized;
    }

    private static string NormalizeTimeZone(string value)
    {
        var normalized = value?.Trim() ?? "";
        if (!TimeZonePattern().IsMatch(normalized)) throw new ArgumentException("Timezone is invalid.");
        return normalized;
    }

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)] private static partial Regex CountryCodePattern();
    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)] private static partial Regex CurrencyPattern();
    [GeneratedRegex("^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)] private static partial Regex LocalePattern();
    [GeneratedRegex("^(?:UTC|[A-Za-z0-9._+-]+(?:/[A-Za-z0-9._+-]+)+)$", RegexOptions.CultureInvariant)] private static partial Regex TimeZonePattern();
}
