namespace SalekhPos.Taxation.Contracts.TaxConfiguration;

public sealed record CreateTaxProfileRequest(string Code, string Name, string CountryCode, bool PricesIncludeTax);
public sealed record TaxProfileResponse(Guid Id, string Code, string Name, string CountryCode, bool PricesIncludeTax, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record TaxProfilePage(IReadOnlyList<TaxProfileResponse> Items, Guid? NextCursor);
public sealed record CreateTaxRateRequest(Guid? BranchId, string CategoryCode, decimal RatePercent, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil);
public sealed record TaxRateResponse(Guid Id, Guid ProfileId, Guid? BranchId, string CategoryCode, decimal RatePercent, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveUntil, bool IsActive, long Version);
public sealed record CalculateTaxRequest(Guid ProfileId, Guid BranchId, string CategoryCode, decimal Amount, DateTimeOffset At);
public sealed record CalculateTaxResponse(Guid ProfileId, Guid? RateId, string CategoryCode, decimal RatePercent, decimal NetAmount, decimal TaxAmount, decimal GrossAmount, bool PricesIncludeTax);
