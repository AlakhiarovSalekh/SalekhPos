namespace SalekhPos.Inventory.Contracts.Stock;

public sealed record CreateStockMovementRequest(Guid ProductId, string Kind, decimal Quantity,
    string? Reason, DateTimeOffset OccurredAt);
public sealed record StockMovementResponse(Guid Id, Guid BranchId, Guid ProductId, string Kind,
    decimal Quantity, string? Reason, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt);
public sealed record StockLevelResponse(Guid ProductId, string Sku, string Name, decimal Quantity);
public sealed record StockPage(IReadOnlyList<StockLevelResponse> Items, Guid? NextCursor);
public sealed record InventoryBranchAccess(Guid BranchId, string Code, string Name, string TimeZoneId,
    bool CanView, bool CanAdjust);
public sealed record InventoryAccessResponse(Guid OrganizationId, IReadOnlyList<InventoryBranchAccess> Branches);
public sealed record CreateWebStockMovementRequest(Guid ProductId, string Kind, string Quantity,
    string? Reason, DateTimeOffset OccurredAt);
