using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class PosWorkspaceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "salekhpos-workspace-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task OnlineOpenCheckoutAndSyncExposeCoherentPresentationState()
    {
        var setup = await Setup();
        var opened = await setup.Workspace.OpenOnlineAsync(default);
        var item = await setup.Workspace.FindByBarcodeAsync("10001", DateTimeOffset.UtcNow, default);
        var sale = await setup.Workspace.CompleteCashSaleAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, 20m,
            [new(setup.ProductId, 1m)]), default);

        Assert.True(opened.IsOnline); Assert.True(opened.CanSell); Assert.Equal(setup.ShiftId, opened.CashSession!.ShiftId);
        Assert.Equal(setup.ProductId, item!.ProductId); Assert.True(sale.Created);
        Assert.Equal(1, setup.Workspace.CurrentState.PendingSales);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Workspace.CloseCashSessionAsync(
            Guid.NewGuid(), 10m, default));

        var synchronized = await setup.Workspace.SynchronizeAsync(default);
        Assert.Equal(0, synchronized.PendingSales); Assert.True(synchronized.CanSell);
        Assert.Equal(1, setup.Transport.Sent);
    }

    [Fact]
    public async Task ReopenedWorkspaceCanSellOfflineFromDurableSessionAndCatalog()
    {
        var setup = await Setup(); await setup.Workspace.OpenOnlineAsync(default);
        var reopened = await Workspace(setup.Scope, setup.Path, new ThrowingSessionSource(),
            new ThrowingCatalog(), setup.Transport);

        var state = await reopened.OpenOfflineAsync(default);
        var sale = await reopened.CompleteCashSaleAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, 20m,
            [new(setup.ProductId, 1m)]), default);

        Assert.False(state.IsOnline); Assert.True(state.CanSell); Assert.True(sale.Created);
        Assert.Equal(1, reopened.CurrentState.PendingSales);
    }

    [Fact]
    public async Task CashMovementAndServerAuthoritativeCloseUpdateWorkspaceState()
    {
        var setup = await Setup(); await setup.Workspace.OpenOnlineAsync(default);

        var movement = await setup.Workspace.RecordCashMovementAsync(Guid.NewGuid(), "cash_in", 5m,
            "Opening float correction", default);
        var closed = await setup.Workspace.CloseCashSessionAsync(Guid.NewGuid(), 5m, default);

        Assert.Equal(setup.ShiftId, movement.ShiftId); Assert.Equal(5m, movement.Amount);
        Assert.Equal(setup.ShiftId, closed.ShiftId); Assert.False(setup.Workspace.CurrentState.CanSell);
        Assert.Null(await new SqliteCashSessionStore(setup.Path).ReadActiveAsync(setup.Scope.OrganizationId,
            setup.Scope.BranchId, setup.Scope.DeviceId, default));
    }

    private async Task<SetupResult> Setup()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var product = Guid.NewGuid();
        var scope = new PosWorkspaceScope(organization, branch, device);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db");
        var openedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var sessions = new SessionSource(new(new(device, branch, register, "active", 1),
            new(shift, branch, register, "open", "GEL", 0m, openedAt), DateTimeOffset.UtcNow));
        var item = new LocalSellableItem(organization, branch, product, Guid.NewGuid(), "SKU-1", "Tea", "EA",
            "10001", 2m, 10m, "GEL", "inclusive", 18m, openedAt, null);
        var remoteCatalog = new CatalogSource(new(organization, branch, DateTimeOffset.UtcNow, [item]));
        var transport = new Transport(); var cash = new CashManagement(register, openedAt);
        return new(scope, path, product, shift,
            await Workspace(scope, path, sessions, remoteCatalog, transport, cash), transport);
    }

    private static async Task<PosWorkspace> Workspace(PosWorkspaceScope scope, string path,
        IRemoteCashSessionSource sessionSource, IRemoteSellableCatalog remoteCatalog, Transport transport,
        IRemoteCashManagement? cashManagement = null)
    {
        var sessionStore = new SqliteCashSessionStore(path); var catalog = new SqliteSellableCatalog(path);
        var sales = await new SqliteLocalSaleStore(path).OpenAsync();
        return new(scope, new(sessionSource, sessionStore), sessionStore, new(remoteCatalog, catalog), catalog,
            new SqliteProjectedSaleCheckout(path),
            new PendingSaleSyncRunner(new PendingSaleSyncDispatcher(sales, transport), new Delay()), sales,
            cashManagement ?? new CashManagement(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private sealed class SessionSource(RemoteCashSessionSnapshot snapshot) : IRemoteCashSessionSource
    {
        public Task<RemoteCashSessionSnapshot> DownloadAsync(Guid organizationId, Guid branchId, Guid deviceId,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }
    private sealed class CatalogSource(SellableCatalogSnapshot snapshot) : IRemoteSellableCatalog
    {
        public Task<SellableCatalogSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }
    private sealed class ThrowingSessionSource : IRemoteCashSessionSource
    {
        public Task<RemoteCashSessionSnapshot> DownloadAsync(Guid organizationId, Guid branchId, Guid deviceId,
            CancellationToken cancellationToken) => throw new HttpRequestException("Offline");
    }
    private sealed class ThrowingCatalog : IRemoteSellableCatalog
    {
        public Task<SellableCatalogSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken) => throw new HttpRequestException("Offline");
    }
    private sealed class Delay : ISyncRetryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Transport : IRemoteSyncTransport
    {
        public int Sent { get; private set; }
        public Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
            LocalOutboxMessage message, CancellationToken cancellationToken)
        {
            Sent++;
            return Task.FromResult(new RemoteSyncAcknowledgement(message.MessageId, message.DeviceId,
                message.SaleId, message.Sequence, 1, message.MessageType, "applied", "applied",
                message.PayloadDigest, DateTimeOffset.UtcNow, false));
        }
    }
    private sealed class CashManagement(Guid registerId, DateTimeOffset openedAt) : IRemoteCashManagement
    {
        public Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid shiftId,
            Guid operationId, string kind, decimal amount, string reason, CancellationToken cancellationToken) =>
            Task.FromResult(new CashMovementResult(Guid.NewGuid(), shiftId, kind, "GEL", amount, reason,
                DateTimeOffset.UtcNow, "cashier"));
        public Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid shiftId,
            Guid operationId, decimal countedCash, CancellationToken cancellationToken) =>
            Task.FromResult(new ClosedCashSessionResult(shiftId, branchId, registerId, "GEL", 0m, 0m, 0m,
                5m, 0m, 5m, countedCash, countedCash - 5m, openedAt, DateTimeOffset.UtcNow, "cashier", "cashier"));
    }
    private sealed record SetupResult(PosWorkspaceScope Scope, string Path, Guid ProductId, Guid ShiftId,
        PosWorkspace Workspace, Transport Transport);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
