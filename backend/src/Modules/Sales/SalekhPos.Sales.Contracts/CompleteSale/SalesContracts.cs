namespace SalekhPos.Sales.Contracts.CompleteSale;

public sealed record CompleteCashSaleLineRequest(Guid ProductId, decimal Quantity);
public sealed record CompleteCashSaleRequest(IReadOnlyList<CompleteCashSaleLineRequest> Lines, decimal CashReceived);
public sealed record CompletedSaleLineResponse(int LineNumber, Guid ProductId, Guid PriceId, decimal Quantity,
    decimal UnitAmount, string Currency, string TaxMode, decimal TaxRate, decimal NetAmount,
    decimal TaxAmount, decimal GrossAmount);
public sealed record CompletedSaleResponse(Guid Id, Guid BranchId, string Currency, decimal NetTotal,
    decimal TaxTotal, decimal GrandTotal, decimal CashReceived, decimal ChangeDue, DateTimeOffset CompletedAt,
    IReadOnlyList<CompletedSaleLineResponse> Lines);
