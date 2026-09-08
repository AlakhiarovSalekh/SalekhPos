namespace SalekhPos.Inventory.Contracts.Stock;

public sealed record CreateStockMovementRequest(Guid ProductId, string Kind, decimal Quantity,
    string? Reason, DateTimeOffset OccurredAt);
public sealed record StockMovementResponse(Guid Id, Guid BranchId, Guid ProductId, string Kind,
    decimal Quantity, string? Reason, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt);
public sealed record StockLevelResponse(Guid ProductId, string Sku, string Name, decimal Quantity);
public sealed record StockPage(IReadOnlyList<StockLevelResponse> Items, Guid? NextCursor);
