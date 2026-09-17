namespace SalekhPos.Accounting.Contracts.Journals;

public sealed record AccountingSummaryResponse(
    Guid BranchId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Currency,
    int CompletedSales,
    decimal SalesNet,
    decimal SalesTax,
    decimal SalesGross,
    int CompletedReturns,
    decimal Refunds,
    decimal NetReceipts,
    decimal CashIn,
    decimal CashOut,
    int ApprovedPurchaseOrders,
    decimal PurchaseCommitments,
    int ClosedShifts,
    decimal ShiftVariance);

public sealed record AccountingJournalItemResponse(
    Guid SourceId,
    string Kind,
    DateTimeOffset OccurredAt,
    string Currency,
    decimal? NetAmount,
    decimal? TaxAmount,
    decimal GrossAmount,
    decimal CashEffect);

public sealed record AccountingJournalPage(
    IReadOnlyList<AccountingJournalItemResponse> Items,
    string? NextCursor);
