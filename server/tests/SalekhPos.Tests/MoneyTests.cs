using SalekhPos.SharedKernel;
using Xunit;

namespace SalekhPos.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void DecimalAmountsAddExactly()
    {
        Assert.Equal(new Money(0.3m, "GEL"), new Money(0.1m, "GEL").Add(new Money(0.2m, "GEL")));
    }

    [Theory]
    [InlineData("GEL")]
    [InlineData("JPY")]
    [InlineData("KWD")]
    public void ConstructionDoesNotAssumeTwoDecimalPlaces(string currency)
    {
        Assert.Equal(1.2345m, new Money(1.2345m, currency).Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gel")]
    [InlineData(" GEL")]
    [InlineData("GEL ")]
    [InlineData("GE")]
    [InlineData("G3L")]
    [InlineData("₾EL")]
    public void InvalidCurrencySyntaxIsRejected(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));
    }

    [Fact]
    public void NullCurrencyIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Money(1m, null!));
    }

    [Fact]
    public void CurrencyMismatchIsRejectedForBothOperations()
    {
        var amount = new Money(10m, "GEL");
        var other = new Money(10m, "USD");
        Assert.Throws<InvalidOperationException>(() => amount.Add(other));
        Assert.Throws<InvalidOperationException>(() => amount.Subtract(other));
    }

    [Fact]
    public void ReversalCanProduceNegativeAmountWithoutChangingOriginal()
    {
        var original = new Money(5m, "GEL");
        Assert.Equal(new Money(-2m, "GEL"), original.Subtract(new Money(7m, "GEL")));
        Assert.Equal(5m, original.Amount);
    }

    [Fact]
    public void OverflowFailsInsteadOfWrappingOrClamping()
    {
        Assert.Throws<OverflowException>(() => new Money(decimal.MaxValue, "GEL").Add(new Money(1m, "GEL")));
        Assert.Throws<OverflowException>(() => new Money(decimal.MinValue, "GEL").Subtract(new Money(1m, "GEL")));
    }

    [Fact]
    public void PrecisionLossIsRejectedEvenWhenDecimalDoesNotOverflow()
    {
        var large = new Money(decimal.MaxValue, "GEL");
        Assert.Throws<ArithmeticException>(() => large.Add(new Money(0.1m, "GEL")));
        Assert.Throws<ArithmeticException>(() => large.Subtract(new Money(0.1m, "GEL")));
    }

    [Fact]
    public void OppositeAmountsCancelAtMaximumMagnitude()
    {
        Assert.Equal(new Money(0m, "GEL"),
            new Money(decimal.MaxValue, "GEL").Add(new Money(-decimal.MaxValue, "GEL")));
    }

    [Fact]
    public void NullOperandIsRejected()
    {
        var amount = new Money(1m, "GEL");
        Assert.Throws<ArgumentNullException>(() => amount.Add(null!));
        Assert.Throws<ArgumentNullException>(() => amount.Subtract(null!));
    }

    [Fact]
    public void EqualityIncludesCurrency()
    {
        Assert.NotEqual(new Money(1m, "GEL"), new Money(1m, "USD"));
        Assert.Equal(new Money(1.0m, "GEL"), new Money(1.00m, "GEL"));
    }
}
