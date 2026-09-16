using SalekhPos.Taxation.Contracts.TaxConfiguration;
using SalekhPos.Taxation.Domain.TaxProfiles;
using SalekhPos.Taxation.Domain.TaxRates;

namespace SalekhPos.Taxation.Application.TaxConfiguration;

public sealed record TaxIdentity(string Issuer, string Subject)
{
    public void Validate() { if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl) || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl)) throw new ArgumentException("Tax identity is invalid."); }
}

public sealed record CreateTaxProfileCommand(Guid OrganizationId, Guid ProfileId, Guid OperationId, string Code, string Name, string CountryCode, bool PricesIncludeTax)
{
    public TaxProfile ToProfile() => new(OrganizationId, ProfileId, Code, Name, CountryCode, PricesIncludeTax);
}
public sealed record CreateTaxRateCommand(Guid OrganizationId, Guid RateId, Guid OperationId, Guid ProfileId, Guid? BranchId, string CategoryCode, decimal RatePercent, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil)
{
    public TaxRate ToRate() => new(OrganizationId, RateId, ProfileId, BranchId, CategoryCode, RatePercent, EffectiveFrom, EffectiveUntil);
}
public sealed record TaxProfileWriteResult(TaxProfileResponse Profile, bool Created);
public sealed record TaxRateWriteResult(TaxRateResponse Rate, bool Created);
public interface ITaxConfiguration
{
    Task<TaxProfileWriteResult> CreateProfileAsync(TaxIdentity identity, CreateTaxProfileCommand command, CancellationToken ct);
    Task<TaxProfilePage> ListProfilesAsync(TaxIdentity identity, Guid organizationId, int pageSize, Guid? after, CancellationToken ct);
    Task<TaxRateWriteResult> CreateRateAsync(TaxIdentity identity, CreateTaxRateCommand command, CancellationToken ct);
    Task<IReadOnlyList<TaxRateResponse>> ListRatesAsync(TaxIdentity identity, Guid organizationId, Guid profileId, CancellationToken ct);
    Task<CalculateTaxResponse> CalculateAsync(TaxIdentity identity, Guid organizationId, CalculateTaxRequest request, CancellationToken ct);
}

public sealed class TaxDeniedException : Exception;
public sealed class TaxUnavailableException : Exception;
public sealed class TaxConflictException : Exception;
public sealed class TaxNotFoundException : Exception;
