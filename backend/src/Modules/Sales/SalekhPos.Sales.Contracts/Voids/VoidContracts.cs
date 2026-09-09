namespace SalekhPos.Sales.Contracts.Voids;

public sealed record VoidSaleRequest(Guid SaleId, string Reason);
public sealed record VoidedSaleLineResponse(int LineNumber, Guid ProductId, decimal Quantity,
    Guid InventoryMovementId);
public sealed record VoidedSaleResponse(Guid Id, Guid SaleId, Guid BranchId, string Currency, decimal Amount,
    string Reason, DateTimeOffset VoidedAt, IReadOnlyList<VoidedSaleLineResponse> Lines);
