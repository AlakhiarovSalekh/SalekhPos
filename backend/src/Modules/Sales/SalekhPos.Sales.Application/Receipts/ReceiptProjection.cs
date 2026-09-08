using SalekhPos.Sales.Contracts.CompleteSale;
using SalekhPos.Sales.Contracts.Receipts;

namespace SalekhPos.Sales.Application.Receipts;

public static class ReceiptProjection
{
    public static ReceiptResponse From(CompletedSaleResponse sale) => new(
        sale.Id.ToString("N").ToUpperInvariant(), sale.Id, sale.BranchId, sale.CompletedAt, sale.Currency,
        sale.NetTotal, sale.TaxTotal, sale.GrandTotal, sale.CashReceived, sale.ChangeDue,
        [.. sale.Lines.Select(line => new ReceiptLineResponse(line.LineNumber, line.ProductId, line.Quantity,
            line.UnitAmount, line.TaxMode, line.TaxRate, line.NetAmount, line.TaxAmount, line.GrossAmount))
        ]);
}
