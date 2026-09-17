namespace SalekhPos.Desktop.Application.Management;

public sealed record AccountingSummary(Guid BranchId, DateTimeOffset From, DateTimeOffset To,
    string? Currency, int CompletedSales, decimal SalesNet, decimal SalesTax, decimal SalesGross,
    int CompletedReturns, decimal Refunds, decimal NetReceipts, decimal CashIn, decimal CashOut,
    int ApprovedPurchaseOrders, decimal PurchaseCommitments, int ClosedShifts, decimal ShiftVariance);

public sealed record AccountingJournalItem(Guid SourceId, string Kind, DateTimeOffset OccurredAt,
    string Currency, decimal? NetAmount, decimal? TaxAmount, decimal GrossAmount, decimal CashEffect);

public sealed record AccountingJournalPage(IReadOnlyList<AccountingJournalItem> Items, string? NextCursor);

public interface IAccountingViewer
{
    Task<AccountingSummary> ReadSummaryAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<AccountingJournalPage> ReadJournalAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, int pageSize, string? cursor,
        CancellationToken cancellationToken);
}
