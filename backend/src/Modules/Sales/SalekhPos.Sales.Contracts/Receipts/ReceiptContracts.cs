namespace SalekhPos.Sales.Contracts.Receipts;

public sealed record ReceiptLineResponse(int LineNumber, Guid ProductId, decimal Quantity, decimal UnitAmount,
    string TaxMode, decimal TaxRate, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

public sealed record ReceiptResponse(string ReceiptNumber, Guid SaleId, Guid BranchId, DateTimeOffset IssuedAt,
    string Currency, decimal NetTotal, decimal TaxTotal, decimal GrandTotal, decimal CashReceived,
    decimal ChangeDue, IReadOnlyList<ReceiptLineResponse> Lines);
