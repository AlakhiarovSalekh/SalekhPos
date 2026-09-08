namespace SalekhPos.Payments.Domain.Payments;

public sealed record CompletedCashPayment
{
    public Guid Id { get; }
    public Guid SaleId { get; }
    public Guid BranchId { get; }
    public string Currency { get; }
    public decimal Amount { get; }
    public decimal Tendered { get; }
    public decimal Change { get; }
    public DateTimeOffset CompletedAt { get; }

    public CompletedCashPayment(Guid id, Guid saleId, Guid branchId, string currency, decimal amount,
        decimal tendered, decimal change, DateTimeOffset completedAt)
    {
        if (id == Guid.Empty || saleId == Guid.Empty || branchId == Guid.Empty || currency.Length != 3
            || amount < 0 || tendered < amount || change != tendered - amount)
            throw new ArgumentException("Completed cash payment is invalid.");
        Id = id; SaleId = saleId; BranchId = branchId; Currency = currency; Amount = amount;
        Tendered = tendered; Change = change; CompletedAt = completedAt;
    }
}
