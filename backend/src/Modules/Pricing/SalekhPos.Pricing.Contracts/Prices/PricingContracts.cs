namespace SalekhPos.Pricing.Contracts.Prices;

public sealed record SchedulePriceRequest(Guid ProductId, Guid? BranchId, decimal Amount, string Currency,
    string TaxMode, decimal TaxRate, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
public sealed record PriceResponse(Guid Id, Guid ProductId, Guid? BranchId, decimal Amount, string Currency,
    string TaxMode, decimal TaxRate, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, DateTimeOffset CreatedAt);
public sealed record ResolvedPriceResponse(Guid PriceId, Guid ProductId, Guid? BranchId, decimal Amount,
    string Currency, string TaxMode, decimal TaxRate, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
