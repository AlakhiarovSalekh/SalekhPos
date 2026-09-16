namespace SalekhPos.Taxation.Domain.TaxRates;

public sealed record TaxRate
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid ProfileId { get; }
    public Guid? BranchId { get; }
    public string CategoryCode { get; }
    public decimal RatePercent { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? EffectiveUntil { get; }

    public TaxRate(Guid organizationId, Guid id, Guid profileId, Guid? branchId, string categoryCode,
        decimal ratePercent, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || profileId == Guid.Empty) throw new ArgumentException("Tax rate identity is invalid.");
        if (ratePercent < 0 || ratePercent > 100 || decimal.Round(ratePercent, 6) != ratePercent) throw new ArgumentException("Tax rate is invalid.");
        if (effectiveFrom == default || effectiveFrom.Offset != TimeSpan.Zero || effectiveUntil is { Offset: var offset } && offset != TimeSpan.Zero || effectiveUntil <= effectiveFrom)
            throw new ArgumentException("Tax rate window is invalid.");
        var code = categoryCode?.Trim().ToUpperInvariant() ?? "";
        if (code.Length is < 1 or > 40 || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new ArgumentException("Tax category is invalid.");
        OrganizationId = organizationId; Id = id; ProfileId = profileId; BranchId = branchId; CategoryCode = code;
        RatePercent = ratePercent; EffectiveFrom = effectiveFrom; EffectiveUntil = effectiveUntil;
    }
}
