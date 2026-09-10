using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class ProjectedSaleCheckoutTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "salekhpos-checkout-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CompletionReservesStockWithSaleAndExactReplayDoesNotReserveTwice()
    {
        var setup = await Setup(2m); var command = Command(setup, 1m);
        var created = await setup.Checkout.CompleteAsync(command, default);
        var replay = await setup.Checkout.CompleteAsync(command, default);

        Assert.True(created.Created); Assert.False(replay.Created); Assert.Equal(created.Message.MessageId, replay.Message.MessageId);
        Assert.Equal(1m, (await setup.Catalog.FindByProductAsync(setup.OrganizationId, setup.BranchId,
            setup.ProductId, command.CompletedAt, default))!.StockQuantity);
        Assert.Single(await setup.Store.ReadPendingAsync(command.DeviceId, 10, default));
    }

    [Fact]
    public async Task InsufficientStockRollsBackSaleReservationAndOutbox()
    {
        var setup = await Setup(1m); var command = Command(setup, 2m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Checkout.CompleteAsync(command, default));

        Assert.Equal(1m, (await setup.Catalog.FindByProductAsync(setup.OrganizationId, setup.BranchId,
            setup.ProductId, command.CompletedAt, default))!.StockQuantity);
        Assert.Empty(await setup.Store.ReadPendingAsync(command.DeviceId, 10, default));
    }

    [Fact]
    public async Task ConcurrentCheckoutsCannotOversellProjectedStock()
    {
        var setup = await Setup(1m); var first = Command(setup, 1m); var second = Command(setup, 1m) with { SaleId = Guid.NewGuid() };
        var attempts = await Task.WhenAll(Attempt(first), Attempt(second));

        Assert.Equal(1, attempts.Count(x => x)); Assert.Equal(0m,
            (await setup.Catalog.FindByProductAsync(setup.OrganizationId, setup.BranchId, setup.ProductId,
                first.CompletedAt, default))!.StockQuantity);
        Assert.Single(await setup.Store.ReadPendingAsync(first.DeviceId, 10, default));

        async Task<bool> Attempt(CompleteProjectedSaleCommand command)
        {
            try { await setup.Checkout.CompleteAsync(command, default); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    [Fact]
    public async Task RejectedResultReleasesReservationExactlyOnceAndAppliedResultKeepsDeduction()
    {
        var rejected = await Setup(1m); var rejectedSale = await rejected.Checkout.CompleteAsync(Command(rejected, 1m), default);
        var acceptedAt = DateTimeOffset.UtcNow;
        await rejected.Store.MarkResultAsync(rejectedSale.Message.MessageId, rejectedSale.Message.PayloadDigest,
            "rejected", "price_conflict", acceptedAt, default);
        await rejected.Store.MarkResultAsync(rejectedSale.Message.MessageId, rejectedSale.Message.PayloadDigest,
            "rejected", "price_conflict", acceptedAt, default);
        Assert.Equal(1m, (await rejected.Catalog.FindByProductAsync(rejected.OrganizationId, rejected.BranchId,
            rejected.ProductId, rejectedSale.Sale.CompletedAt, default))!.StockQuantity);

        var applied = await Setup(1m); var appliedSale = await applied.Checkout.CompleteAsync(Command(applied, 1m), default);
        await applied.Store.MarkResultAsync(appliedSale.Message.MessageId, appliedSale.Message.PayloadDigest,
            "applied", "applied", DateTimeOffset.UtcNow, default);
        Assert.Equal(0m, (await applied.Catalog.FindByProductAsync(applied.OrganizationId, applied.BranchId,
            applied.ProductId, appliedSale.Sale.CompletedAt, default))!.StockQuantity);
    }

    [Fact]
    public async Task CatalogReplacementIsBlockedWhileReservationIsPending()
    {
        var setup = await Setup(1m); var sale = await setup.Checkout.CompleteAsync(Command(setup, 1m), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Catalog.ApplyAsync(new(setup.OrganizationId,
            setup.BranchId, DateTimeOffset.UtcNow.AddMinutes(1), [setup.Item]), default));
        Assert.Single(await setup.Store.ReadPendingAsync(sale.Message.DeviceId, 10, default));
    }

    private async Task<SetupResult> Setup(decimal stock)
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var productId = Guid.NewGuid();
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db"); var catalog = new SqliteSellableCatalog(path);
        var item = new LocalSellableItem(organizationId, branchId, productId, Guid.NewGuid(), "SKU", "Tea", "EA",
            "12345", stock, 10m, "GEL", "inclusive", 18m, DateTimeOffset.UtcNow.AddDays(-1), null);
        await catalog.ApplyAsync(new(organizationId, branchId, DateTimeOffset.UtcNow, [item]), default);
        var store = await new SqliteLocalSaleStore(path).OpenAsync();
        return new(organizationId, branchId, productId, item, catalog, new SqliteProjectedSaleCheckout(path), store);
    }
    private static CompleteProjectedSaleCommand Command(SetupResult setup, decimal quantity) => new(
        setup.OrganizationId, setup.BranchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        DateTimeOffset.UtcNow, 100m, [new(setup.ProductId, quantity)]);
    private sealed record SetupResult(Guid OrganizationId, Guid BranchId, Guid ProductId, LocalSellableItem Item,
        SqliteSellableCatalog Catalog, SqliteProjectedSaleCheckout Checkout, ILocalSaleStore Store);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
