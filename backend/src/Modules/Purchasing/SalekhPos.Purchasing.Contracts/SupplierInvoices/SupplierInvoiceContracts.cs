namespace SalekhPos.Purchasing.Contracts.SupplierInvoices;

public sealed record SupplierInvoiceLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);

public sealed record CreateSupplierInvoiceRequest(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    string InvoiceNumber,
    string Currency,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    IReadOnlyList<SupplierInvoiceLineRequest> Lines);

public sealed record SupplierInvoiceLineResponse(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record SupplierInvoiceResponse(
    Guid Id,
    Guid BranchId,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    string InvoiceNumber,
    string Currency,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Status,
    decimal Total,
    long Version,
    IReadOnlyList<SupplierInvoiceLineResponse> Lines);
