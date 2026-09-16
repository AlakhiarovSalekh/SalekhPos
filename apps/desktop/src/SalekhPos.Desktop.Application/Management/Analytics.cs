namespace SalekhPos.Desktop.Application.Management;

public sealed record AnalyticsOverviewSummary(Guid BranchId, DateTimeOffset From, DateTimeOffset To,
    string? Currency, int CompletedSales, decimal GrossSales, int CompletedReturns, decimal Refunds,
    decimal NetRevenue, decimal AverageTicket, int DistinctProductsSold, int PositiveStockProducts,
    int ZeroStockProducts, int NegativeStockProducts);
public sealed record AnalyticsTrendPoint(DateTimeOffset BucketStart, string? Currency,
    int CompletedSales, decimal GrossSales, decimal Refunds, decimal NetRevenue);
public sealed record AnalyticsTrendSummary(Guid BranchId, DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<AnalyticsTrendPoint> Points);
public sealed record StoreComparisonSummary(Guid BranchId, string? Currency, int CompletedSales,
    decimal GrossSales, decimal Refunds, decimal NetRevenue, decimal AverageTicket);
public sealed record StoreComparisonResult(DateTimeOffset From, DateTimeOffset To,
    IReadOnlyList<StoreComparisonSummary> Items);

public interface IAnalyticsViewer
{
    Task<AnalyticsOverviewSummary> ReadOverviewAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<AnalyticsTrendSummary> ReadTrendAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<StoreComparisonResult> CompareStoresAsync(Guid organizationId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
