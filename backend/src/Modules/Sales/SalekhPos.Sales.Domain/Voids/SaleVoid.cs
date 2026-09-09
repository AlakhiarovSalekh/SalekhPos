namespace SalekhPos.Sales.Domain.Voids;

public sealed record SaleVoid
{
    public Guid Id { get; }
    public Guid SaleId { get; }
    public string Reason { get; }
    public SaleVoid(Guid id, Guid saleId, string reason)
    {
        reason = reason.Trim();
        if (id == Guid.Empty || saleId == Guid.Empty || reason.Length is < 3 or > 500
            || reason.Any(char.IsControl)) throw new ArgumentException("Sale void is invalid.");
        Id = id; SaleId = saleId; Reason = reason;
    }
}
