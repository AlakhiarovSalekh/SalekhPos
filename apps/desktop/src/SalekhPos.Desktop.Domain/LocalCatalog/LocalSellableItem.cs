namespace SalekhPos.Desktop.Domain.LocalCatalog;

public sealed record LocalSellableItem
{
    public LocalSellableItem(Guid organizationId, Guid branchId, Guid productId, Guid priceId, string sku,
        string name, string unitCode, string? barcode, decimal stockQuantity, decimal unitAmount,
        string currency, string taxMode, decimal taxRate, DateTimeOffset priceValidFrom,
        DateTimeOffset? priceValidUntil)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || productId == Guid.Empty || priceId == Guid.Empty)
            throw new ArgumentException("Sellable item identifiers are required.");
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > 64 || string.IsNullOrWhiteSpace(name)
            || name.Length > 256 || string.IsNullOrWhiteSpace(unitCode) || unitCode.Length > 16
            || barcode?.Length > 64 || barcode is not null && string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException("Sellable item identity is invalid.");
        if (Scale(stockQuantity) > 6 || Scale(unitAmount) > 6 || Scale(taxRate) > 6
            || unitAmount < 0 || taxRate is < 0 or > 1)
            throw new ArgumentException("Sellable item financial values are invalid.");
        if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z')
            || taxMode is not ("inclusive" or "exclusive"))
            throw new ArgumentException("Sellable item price metadata is invalid.");
        if (priceValidFrom == default || priceValidFrom.Offset != TimeSpan.Zero
            || priceValidUntil is { Offset: not { Ticks: 0 } } || priceValidUntil <= priceValidFrom)
            throw new ArgumentException("Sellable item price interval is invalid.");

        OrganizationId = organizationId; BranchId = branchId; ProductId = productId; PriceId = priceId;
        Sku = sku; Name = name; UnitCode = unitCode; Barcode = barcode; StockQuantity = stockQuantity;
        UnitAmount = unitAmount; Currency = currency; TaxMode = taxMode; TaxRate = taxRate;
        PriceValidFrom = priceValidFrom; PriceValidUntil = priceValidUntil;
    }

    private static byte Scale(decimal value) => (byte)((decimal.GetBits(value)[3] >> 16) & 0x7F);

    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid ProductId { get; }
    public Guid PriceId { get; }
    public string Sku { get; }
    public string Name { get; }
    public string UnitCode { get; }
    public string? Barcode { get; }
    public decimal StockQuantity { get; }
    public decimal UnitAmount { get; }
    public string Currency { get; }
    public string TaxMode { get; }
    public decimal TaxRate { get; }
    public DateTimeOffset PriceValidFrom { get; }
    public DateTimeOffset? PriceValidUntil { get; }
}
