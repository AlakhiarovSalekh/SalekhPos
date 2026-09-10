namespace SalekhPos.Desktop.Domain.LocalSales;

public sealed record LocalSaleLine
{
    public Guid ProductId { get; }
    public Guid PriceId { get; }
    public decimal Quantity { get; }
    public decimal UnitAmount { get; }
    public string Currency { get; }
    public string TaxMode { get; }
    public decimal TaxRate { get; }
    public decimal NetAmount { get; }
    public decimal TaxAmount { get; }
    public decimal GrossAmount { get; }
    public LocalSaleLine(Guid productId, Guid priceId, decimal quantity, decimal unitAmount, string currency, string taxMode, decimal taxRate)
    {
        if (productId == Guid.Empty || priceId == Guid.Empty) throw new ArgumentException("Line identity is invalid.");
        if (quantity <= 0 || quantity > 99999999999999.999999m || decimal.Round(quantity, 6) != quantity) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitAmount <= 0 || unitAmount > 99999999999999.999999m || decimal.Round(unitAmount, 6) != unitAmount) throw new ArgumentOutOfRangeException(nameof(unitAmount));
        if (currency is null || currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z')) throw new ArgumentException("Currency is invalid.");
        if (taxMode is not ("inclusive" or "exclusive") || taxRate is < 0 or > 100 || decimal.Round(taxRate, 4) != taxRate) throw new ArgumentException("Tax is invalid.");
        ProductId = productId; PriceId = priceId; Quantity = quantity; UnitAmount = unitAmount; Currency = currency; TaxMode = taxMode; TaxRate = taxRate;
        var extended = Round(checked(quantity * unitAmount)); NetAmount = taxMode == "exclusive" ? extended : taxRate == 0 ? extended : Round(extended / (1m + taxRate / 100m));
        TaxAmount = taxMode == "exclusive" ? Round(checked(extended * taxRate / 100m)) : checked(extended - NetAmount); GrossAmount = taxMode == "exclusive" ? checked(NetAmount + TaxAmount) : extended;
    }
    public static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.ToEven);
}

public sealed record LocalSale
{
    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid DeviceId { get; }
    public Guid SaleId { get; }
    public Guid ShiftId { get; }
    public Guid RegisterId { get; }
    public DateTimeOffset CompletedAt { get; }
    public string Currency { get; }
    public decimal CashReceived { get; }
    public decimal NetTotal { get; }
    public decimal TaxTotal { get; }
    public decimal GrandTotal { get; }
    public decimal ChangeDue { get; }
    public IReadOnlyList<LocalSaleLine> Lines { get; }
    public LocalSale(Guid organizationId, Guid branchId, Guid deviceId, Guid saleId, Guid shiftId, Guid registerId,
        DateTimeOffset completedAt, decimal cashReceived, IReadOnlyList<LocalSaleLine> lines)
    {
        if (new[] { organizationId, branchId, deviceId, saleId, shiftId, registerId }.Any(x => x == Guid.Empty)) throw new ArgumentException("Sale identity is invalid.");
        if (completedAt == default || completedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Completion time must be UTC.");
        if (lines is null || lines.Count is < 1 or > 500 || lines.Select(x => x.ProductId).Distinct().Count() != lines.Count) throw new ArgumentException("Sale lines are invalid.");
        Currency = lines[0].Currency; if (lines.Any(x => x.Currency != Currency)) throw new ArgumentException("Sale currency is inconsistent.");
        OrganizationId = organizationId; BranchId = branchId; DeviceId = deviceId; SaleId = saleId; ShiftId = shiftId; RegisterId = registerId; CompletedAt = completedAt;
        Lines = [.. lines]; NetTotal = Sum(lines.Select(x => x.NetAmount)); TaxTotal = Sum(lines.Select(x => x.TaxAmount)); GrandTotal = Sum(lines.Select(x => x.GrossAmount));
        if (cashReceived < GrandTotal || decimal.Round(cashReceived, 6) != cashReceived) throw new ArgumentOutOfRangeException(nameof(cashReceived)); CashReceived = cashReceived; ChangeDue = LocalSaleLine.Round(cashReceived - GrandTotal);
    }
    private static decimal Sum(IEnumerable<decimal> values) { var total = 0m; foreach (var value in values) total = checked(total + value); return total; }
}
