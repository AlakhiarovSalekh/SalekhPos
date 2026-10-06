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

public sealed record ReceivePurchaseOrderLineRequest(Guid ProductId, decimal Quantity);
public sealed record ReceivePurchaseOrderRequest(long ExpectedVersion, string? Reference,
    DateTimeOffset ReceivedAt, IReadOnlyList<ReceivePurchaseOrderLineRequest> Lines);

public sealed record PurchaseOrderReceivingLineResponse(Guid ProductId, decimal OrderedQuantity,
    decimal ReceivedQuantity, decimal RemainingQuantity);
public sealed record PurchaseOrderReceivingStateResponse(Guid OrderId, string Status, long Version,
    IReadOnlyList<PurchaseOrderReceivingLineResponse> Lines);

public sealed record PurchaseReceiptLineResponse(Guid ProductId, decimal Quantity, Guid MovementId);
public sealed record PurchaseReceiptResponse(Guid Id, Guid OrderId, Guid BranchId, string? Reference,
    DateTimeOffset ReceivedAt, DateTimeOffset CreatedAt, string ReceivedBySubject,
    IReadOnlyList<PurchaseReceiptLineResponse> Lines);
public sealed record ReceivePurchaseOrderResponse(PurchaseReceiptResponse Receipt, PurchaseOrderResponse Order);
