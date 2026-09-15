namespace SalekhPos.Purchasing.Contracts.PurchaseOrders;

public sealed record CreatePurchaseOrderLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record CreatePurchaseOrderRequest(Guid SupplierId, string Currency,
    string? Reference, IReadOnlyList<CreatePurchaseOrderLineRequest> Lines);
public sealed record ChangePurchaseOrderStatusRequest(long ExpectedVersion);

public sealed record PurchaseOrderLineResponse(Guid ProductId, decimal Quantity, decimal UnitCost,
    decimal LineTotal);
public sealed record PurchaseOrderResponse(Guid Id, Guid BranchId, Guid SupplierId, string Status,
    string Currency, string? Reference, decimal Total, long Version, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<PurchaseOrderLineResponse> Lines);
public sealed record PurchaseOrderPage(IReadOnlyList<PurchaseOrderResponse> Items, Guid? NextCursor);
