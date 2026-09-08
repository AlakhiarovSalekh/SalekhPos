using SalekhPos.Sales.Domain.SaleItems;
using SalekhPos.Sales.Domain.Sales;
using Xunit;

namespace SalekhPos.Tests;

public sealed class SaleCalculationTests
{
    [Fact]
    public void InclusiveTaxProducesAuditableComponents()
    {
        var line = new SaleLineCalculation(Guid.NewGuid(), Guid.NewGuid(), 2m, 11.80m, "GEL",
            AppliedTaxMode.Inclusive, 18m);
        Assert.Equal(20m, line.NetAmount);
        Assert.Equal(3.6m, line.TaxAmount);
        Assert.Equal(23.6m, line.GrossAmount);
    }

    [Fact]
    public void CompletedSaleRequiresEnoughCashAndOneCurrency()
    {
        var line = new SaleLineCalculation(Guid.NewGuid(), Guid.NewGuid(), 1m, 10m, "GEL",
            AppliedTaxMode.Exclusive, 18m);
        var sale = new CompletedSale(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [line], 12m, DateTimeOffset.UtcNow);
        Assert.Equal(11.8m, sale.GrandTotal);
        Assert.Equal(0.2m, sale.ChangeDue);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompletedSale(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), [line], 11m, DateTimeOffset.UtcNow));
    }
}
