namespace SalekhPos.Analytics.Contracts.Analytics;

public sealed record AnalyticsOverviewResponse(
    Guid BranchId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Currency,
    int CompletedSales,
    decimal GrossSales,
    int CompletedReturns,
    decimal Refunds,
    decimal NetRevenue,
    decimal AverageTicket,
    int DistinctProductsSold,
    int PositiveStockProducts,
    int ZeroStockProducts,
    int NegativeStockProducts);

public sealed record SalesTrendPointResponse(
    DateTimeOffset BucketStart,
    string? Currency,
    int CompletedSales,
    decimal GrossSales,
    decimal Refunds,
    decimal NetRevenue);
public sealed record SalesTrendResponse(
    Guid BranchId,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<SalesTrendPointResponse> Points);

public sealed record StoreComparisonRowResponse(
    Guid BranchId,
    string? Currency,
    int CompletedSales,
    decimal GrossSales,
    decimal Refunds,
    decimal NetRevenue,
    decimal AverageTicket);

public sealed record StoreComparisonResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<StoreComparisonRowResponse> Items);
