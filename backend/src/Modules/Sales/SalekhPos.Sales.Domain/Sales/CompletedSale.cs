using SalekhPos.Sales.Domain.SaleItems;

namespace SalekhPos.Sales.Domain.Sales;

public sealed record CompletedSale
{
    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid Id { get; }
    public IReadOnlyList<SaleLineCalculation> Lines { get; }
    public string Currency { get; }
    public decimal NetTotal { get; }
    public decimal TaxTotal { get; }
    public decimal GrandTotal { get; }
    public decimal CashReceived { get; }
    public decimal ChangeDue { get; }
    public DateTimeOffset CompletedAt { get; }

    public CompletedSale(Guid organizationId, Guid branchId, Guid id, IReadOnlyList<SaleLineCalculation> lines,
        decimal cashReceived, DateTimeOffset completedAt)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Sale ID is invalid.");
        if (lines is null || lines.Count is < 1 or > 500 || lines.Select(line => line.ProductId).Distinct().Count() != lines.Count)
            throw new ArgumentException("Sale lines are invalid.", nameof(lines));
        Currency = lines[0].Currency;
        if (lines.Any(line => line.Currency != Currency)) throw new ArgumentException("A sale must use one currency.", nameof(lines));
        OrganizationId = organizationId;
        BranchId = branchId;
        Id = id;
        Lines = [.. lines];
        NetTotal = Sum(lines.Select(line => line.NetAmount));
        TaxTotal = Sum(lines.Select(line => line.TaxAmount));
        GrandTotal = Sum(lines.Select(line => line.GrossAmount));
        if (cashReceived < GrandTotal || decimal.Round(cashReceived, 6) != cashReceived)
            throw new ArgumentOutOfRangeException(nameof(cashReceived));
        CashReceived = cashReceived;
        ChangeDue = SaleLineCalculation.Round(cashReceived - GrandTotal);
        if (completedAt == default || completedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Completion time is invalid.");
        CompletedAt = new DateTimeOffset(completedAt.Ticks - completedAt.Ticks % 10, TimeSpan.Zero);
    }

    private static decimal Sum(IEnumerable<decimal> values)
    {
        var total = 0m;
        foreach (var value in values) total = checked(total + value);
        return total;
    }
}
