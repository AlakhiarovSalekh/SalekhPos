using SalekhPos.Localization.Contracts.Settings;
using SalekhPos.Localization.Domain.Settings;

namespace SalekhPos.Localization.Application.Settings;

public sealed record LocalizationIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Localization identity is invalid.");
    }
}

public sealed record UpdateLocalizationCommand(Guid OrganizationId, Guid OperationId,
    string CountryCode, string DefaultLocale, string DefaultCurrency, string TimeZone,
    IReadOnlyList<string> SupportedLocales, int FirstDayOfWeek, long? ExpectedVersion)
{
    public OrganizationLocalization ToSettings() => new(OrganizationId, CountryCode, DefaultLocale,
        DefaultCurrency, TimeZone, SupportedLocales, FirstDayOfWeek);
}

public sealed record LocalizationWriteResult(LocalizationSettingsResponse Settings, bool Applied);
public interface ILocalizationSettings
{
    Task<LocalizationSettingsResponse> ReadAsync(LocalizationIdentity identity,
        Guid organizationId, CancellationToken cancellationToken);
    Task<LocalizationWriteResult> UpdateAsync(LocalizationIdentity identity,
        UpdateLocalizationCommand command, CancellationToken cancellationToken);
}

public sealed class LocalizationDeniedException : Exception;
public sealed class LocalizationUnavailableException : Exception;
public sealed class LocalizationConflictException : Exception;
public sealed class LocalizationNotFoundException : Exception;
