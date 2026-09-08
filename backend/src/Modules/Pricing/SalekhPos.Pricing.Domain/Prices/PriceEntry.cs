namespace SalekhPos.Pricing.Domain.Prices;

public enum TaxMode { Inclusive, Exclusive }

public sealed record PriceAmount
{
    public decimal Amount { get; }
    public string Currency { get; }
    public PriceAmount(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (amount <= 0 || amount > 99999999999999.999999m || decimal.Round(amount, 6) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must contain three uppercase ASCII letters.", nameof(currency));
        Amount = amount;
        Currency = currency;
    }
}

public sealed record PriceEntry
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid ProductId { get; }
    public Guid? BranchId { get; }
    public PriceAmount Price { get; }
    public TaxMode TaxMode { get; }
    public decimal TaxRate { get; }
    public DateTimeOffset ValidFrom { get; }
    public DateTimeOffset? ValidUntil { get; }

    public PriceEntry(Guid organizationId, Guid id, Guid productId, Guid? branchId, decimal amount,
        string currency, TaxMode taxMode, decimal taxRate, DateTimeOffset validFrom, DateTimeOffset? validUntil)
    {
        OrganizationId = Required(organizationId, nameof(organizationId));
        Id = Required(id, nameof(id));
        ProductId = Required(productId, nameof(productId));
        if (branchId == Guid.Empty) throw new ArgumentException("Branch ID is invalid.", nameof(branchId));
        if (!Enum.IsDefined(taxMode)) throw new ArgumentException("Tax mode is invalid.", nameof(taxMode));
        if (taxRate is < 0 or > 100 || decimal.Round(taxRate, 4) != taxRate)
            throw new ArgumentOutOfRangeException(nameof(taxRate));
        if (!Utc(validFrom) || validUntil is not null && (!Utc(validUntil.Value) || validUntil <= validFrom))
            throw new ArgumentException("Price validity is invalid.");
        BranchId = branchId;
        Price = new PriceAmount(amount, currency);
        TaxMode = taxMode;
        TaxRate = taxRate;
        ValidFrom = Microseconds(validFrom);
        ValidUntil = validUntil is null ? null : Microseconds(validUntil.Value);
    }

    private static bool Utc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
    private static DateTimeOffset Microseconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);
    private static Guid Required(Guid value, string parameter) => value == Guid.Empty
        ? throw new ArgumentException("ID must not be empty.", parameter) : value;
}
