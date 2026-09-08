namespace SalekhPos.Sales.Domain.SaleItems;

public enum AppliedTaxMode { Inclusive, Exclusive }

public sealed record SaleLineCalculation
{
    public Guid ProductId { get; }
    public Guid PriceId { get; }
    public decimal Quantity { get; }
    public decimal UnitAmount { get; }
    public string Currency { get; }
    public AppliedTaxMode TaxMode { get; }
    public decimal TaxRate { get; }
    public decimal NetAmount { get; }
    public decimal TaxAmount { get; }
    public decimal GrossAmount { get; }

    public SaleLineCalculation(Guid productId, Guid priceId, decimal quantity, decimal unitAmount,
        string currency, AppliedTaxMode taxMode, decimal taxRate)
    {
        ProductId = Required(productId, nameof(productId));
        PriceId = Required(priceId, nameof(priceId));
        if (quantity <= 0 || quantity > 99999999999999.999999m || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitAmount <= 0 || unitAmount > 99999999999999.999999m || decimal.Round(unitAmount, 6) != unitAmount)
            throw new ArgumentOutOfRangeException(nameof(unitAmount));
        if (currency is null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency is invalid.", nameof(currency));
        if (!Enum.IsDefined(taxMode) || taxRate is < 0 or > 100 || decimal.Round(taxRate, 4) != taxRate)
            throw new ArgumentException("Tax policy is invalid.");
        Quantity = quantity;
        UnitAmount = unitAmount;
        Currency = currency;
        TaxMode = taxMode;
        TaxRate = taxRate;
        var extended = Round(checked(unitAmount * quantity));
        if (taxMode == AppliedTaxMode.Exclusive)
        {
            NetAmount = extended;
            TaxAmount = Round(checked(extended * taxRate / 100m));
            GrossAmount = checked(NetAmount + TaxAmount);
        }
        else
        {
            GrossAmount = extended;
            NetAmount = taxRate == 0 ? extended : Round(extended / (1m + taxRate / 100m));
            TaxAmount = checked(GrossAmount - NetAmount);
        }
    }

    public static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.ToEven);
    private static Guid Required(Guid value, string parameter) => value == Guid.Empty
        ? throw new ArgumentException("ID must not be empty.", parameter) : value;
}
