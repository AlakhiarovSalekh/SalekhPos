namespace SalekhPos.Returns.Domain.Returns;

public sealed record CompletedReturn
{
    public Guid Id { get; }
    public Guid SaleId { get; }
    public string Reason { get; }
    public decimal Amount { get; }
    public DateTimeOffset CompletedAt { get; }
    public CompletedReturn(Guid id, Guid saleId, string reason, decimal amount, DateTimeOffset completedAt)
    {
        reason = reason.Trim();
        if (id == Guid.Empty || saleId == Guid.Empty || reason.Length is < 3 or > 500
            || reason.Any(char.IsControl) || amount <= 0) throw new ArgumentException("Return is invalid.");
        Id = id; SaleId = saleId; Reason = reason; Amount = amount; CompletedAt = completedAt;
    }
}

public sealed record ReturnItem
{
    public Guid ProductId { get; }
    public decimal Quantity { get; }

    public ReturnItem(Guid productId, decimal quantity)
    {
        if (productId == Guid.Empty || quantity <= 0 || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentException("Return item is invalid.");
        ProductId = productId;
        Quantity = quantity;
    }
}
