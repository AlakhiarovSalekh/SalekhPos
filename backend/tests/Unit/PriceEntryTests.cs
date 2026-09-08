using SalekhPos.Pricing.Domain.Prices;
using Xunit;

namespace SalekhPos.Tests;

public sealed class PriceEntryTests
{
    [Theory]
    [InlineData("GEL", TaxMode.Inclusive)]
    [InlineData("USD", TaxMode.Exclusive)]
    public void ValidPricePreservesExplicitFinancialPolicy(string currency, TaxMode mode)
    {
        var entry = new PriceEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 12.345678m,
            currency, mode, 18m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(currency, entry.Price.Currency);
        Assert.Equal(mode, entry.TaxMode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.0000001)]
    public void InvalidAmountsAreRejected(decimal amount) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new PriceEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, amount, "GEL",
            TaxMode.Inclusive, 18m, DateTimeOffset.UtcNow, null));
}
