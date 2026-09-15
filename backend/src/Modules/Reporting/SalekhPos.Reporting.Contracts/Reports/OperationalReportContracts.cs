namespace SalekhPos.Reporting.Contracts.Reports;

public sealed record OperationalSummaryResponse(
    Guid BranchId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Currency,
    int CompletedSales,
    decimal GrossSales,
    int CompletedReturns,
    decimal Refunds,
    decimal NetSales,
    int PurchaseOrders,
    decimal PurchaseOrderValue,
    int OpenShifts);
