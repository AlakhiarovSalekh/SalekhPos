using SalekhPos.Inventory.Domain.StockMovements;
using Xunit;

namespace SalekhPos.Tests;

public sealed class StockMovementTests
{
    [Theory]
    [InlineData(StockMovementKind.Receipt, 1)]
    [InlineData(StockMovementKind.AdjustmentIn, 1)]
    [InlineData(StockMovementKind.Return, 1)]
    [InlineData(StockMovementKind.AdjustmentOut, -1)]
    [InlineData(StockMovementKind.Sale, -1)]
    public void DirectionMatchesMovementSemantics(StockMovementKind kind, int direction)
    {
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, 1.25m, "Count", DateTimeOffset.UtcNow);
        Assert.Equal(direction, movement.Direction);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.0000001)]
    public void InvalidQuantitiesAreRejected(decimal quantity) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StockMovementKind.Receipt, quantity, null, DateTimeOffset.UtcNow));

    [Fact]
    public void OccurrenceTimeUsesDatabasePrecision()
    {
        var instant = new DateTimeOffset(638930000000000007, TimeSpan.Zero);
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StockMovementKind.Receipt, 1m, null, instant);
        Assert.Equal(638930000000000000, movement.OccurredAt.Ticks);
    }
}
