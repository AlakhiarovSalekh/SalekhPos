using SalekhPos.Payments.Domain.Payments;
using Xunit;

namespace SalekhPos.Tests;

public sealed class CompletedCashPaymentTests
{
    [Fact]
    public void CompletedPaymentPreservesTenderAndChange()
    {
        var payment = new CompletedCashPayment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GEL",
            11.80m, 12m, 0.20m, DateTimeOffset.UtcNow);
        Assert.Equal(11.80m, payment.Amount);
        Assert.Equal(0.20m, payment.Change);
    }

    [Fact]
    public void InconsistentCashAmountsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new CompletedCashPayment(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "GEL", 10m, 9m, -1m, DateTimeOffset.UtcNow));
    }
}
