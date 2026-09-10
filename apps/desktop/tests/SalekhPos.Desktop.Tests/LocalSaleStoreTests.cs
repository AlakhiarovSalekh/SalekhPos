using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class LocalSaleStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "salekhpos-desktop-tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task CompletionAndOutboxAreAtomicDurableAndIdempotent()
    {
        var path = Path.Combine(directory, "pos.db"); var command = Sale(Guid.NewGuid());
        var store = await new SqliteLocalSaleStore(path).OpenAsync(); var created = await store.CompleteAsync(command, default);
        Assert.True(created.Created); Assert.Equal(1, created.Message.Sequence); Assert.Equal("pending", created.Message.Status);
        Assert.DoesNotContain("\"messageId\"", created.Message.Payload, StringComparison.OrdinalIgnoreCase);
        var reopened = await new SqliteLocalSaleStore(path).OpenAsync(); var pending = Assert.Single(await reopened.ReadPendingAsync(command.DeviceId, 10, default));
        Assert.Equal(created.Message.MessageId, pending.MessageId); Assert.Equal(created.Message.PayloadDigest, pending.PayloadDigest);
        var replay = await reopened.CompleteAsync(command, default); Assert.False(replay.Created); Assert.Equal(created.Message.MessageId, replay.Message.MessageId);
        await reopened.MarkResultAsync(pending.MessageId, pending.PayloadDigest, "applied", "applied", DateTimeOffset.UtcNow, default);
        Assert.Empty(await reopened.ReadPendingAsync(command.DeviceId, 10, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.CompleteAsync(command with { CashReceived = 4m }, default));
    }
    [Fact]
    public async Task ConcurrentCompletionsReceiveGapFreeDeviceSequences()
    {
        var path = Path.Combine(directory, "race.db"); var device = Guid.NewGuid(); var store = await new SqliteLocalSaleStore(path).OpenAsync();
        var results = await Task.WhenAll(store.CompleteAsync(Sale(device), default), store.CompleteAsync(Sale(device), default));
        Assert.Equal([1L, 2L], [.. results.Select(x => x.Message.Sequence).Order()]);
        Assert.Equal(2, (await store.ReadPendingAsync(device, 10, default)).Count);
    }
    private static CompleteLocalSaleCommand Sale(Guid device) => new(Guid.NewGuid(), Guid.NewGuid(), device,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 3m,
        [new LocalSaleLine(Guid.NewGuid(), Guid.NewGuid(), 1m, 2m, "GEL", "inclusive", 0m)]);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
