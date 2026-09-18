namespace SalekhPos.Billing.Domain.Invoices;

public sealed record InvoiceLine
{
    public InvoiceLine(Guid id, string description, long quantity, decimal unitAmount, decimal taxRate)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invoice line identifier is required.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 240 || description.Any(char.IsControl))
            throw new ArgumentException("Invoice line description is invalid.");
        if (quantity is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(quantity));
        MoneyGuard.Amount(unitAmount, allowZero: true);
        if (taxRate is < 0 or > 100 || decimal.Round(taxRate, 6) != taxRate) throw new ArgumentOutOfRangeException(nameof(taxRate));
        Id = id; Description = description.Trim(); Quantity = quantity; UnitAmount = unitAmount; TaxRate = taxRate;
        NetAmount = decimal.Round(quantity * unitAmount, 2, MidpointRounding.AwayFromZero);
        TaxAmount = decimal.Round(NetAmount * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; }
    public string Description { get; }
    public long Quantity { get; }
    public decimal UnitAmount { get; }
    public decimal TaxRate { get; }
    public decimal NetAmount { get; }
    public decimal TaxAmount { get; }
    public decimal GrossAmount => NetAmount + TaxAmount;
}

internal static class MoneyGuard
{
    public static void Amount(decimal value, bool allowZero = false)
    {
        if ((allowZero ? value < 0 : value <= 0) || value > 999_999_999_999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
