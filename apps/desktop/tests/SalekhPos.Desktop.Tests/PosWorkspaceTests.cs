using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.Shifts;
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

        Assert.False(state.IsOnline); Assert.True(state.IsCatalogProjectionReady); Assert.True(state.CanSell);
        Assert.True(sale.Created);
        Assert.Equal(1, reopened.CurrentState.PendingSales);
    }

    [Fact]
    public async Task OnlineFirstReadinessSeedsCatalogWithoutCashSession()
    {
        var setup = await Setup(hasCashSession: false);

        var state = await setup.Workspace.OpenOnlineAsync(default);

        Assert.True(state.IsOnline);
        Assert.True(state.IsCatalogProjectionReady);
        Assert.Null(state.CashSession);
        Assert.False(state.CanSell);
        Assert.NotNull(await setup.Workspace.FindByBarcodeAsync("10001", DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task FirstRunOfflineWithoutCatalogProjectionFailsClosed()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid();
        var scope = new PosWorkspaceScope(organization, branch, device);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db");
        var capturedAt = DateTimeOffset.UtcNow;
        var sessionStore = new SqliteCashSessionStore(path);
        await sessionStore.ApplyAsync(organization, branch, device,
            new(new(device, branch, register, "active", 1),
                new(shift, branch, register, "open", "GEL", 0m, capturedAt.AddHours(-1)), capturedAt), default);
        var workspace = await Workspace(scope, path, new ThrowingSessionSource(), new ThrowingCatalog(),
            new Transport());

        var exception = await Assert.ThrowsAsync<CatalogProjectionNotReadyException>(() =>
            workspace.OpenOfflineAsync(default));

        Assert.Equal("Verified offline catalog data is not available for this organization and branch.",
            exception.Message);
        Assert.Throws<InvalidOperationException>(() => workspace.CurrentState);
    }

    [Fact]
    public async Task CatalogReadinessCannotBeReusedByAnotherBranch()
    {
        var setup = await Setup();
        await setup.Workspace.OpenOnlineAsync(default);
        var wrongBranchScope = setup.Scope with { BranchId = Guid.NewGuid() };
        var wrongBranch = await Workspace(wrongBranchScope, setup.Path, new ThrowingSessionSource(),
            new ThrowingCatalog(), setup.Transport);

        await Assert.ThrowsAsync<CatalogProjectionNotReadyException>(() =>
            wrongBranch.OpenOfflineAsync(default));
    }

    [Fact]
    public void CanSellRequiresCashSessionAndCatalogReadiness()
    {
        var scope = new PosWorkspaceScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var session = new LocalCashSession(scope.OrganizationId, scope.BranchId, scope.DeviceId,
            Guid.NewGuid(), Guid.NewGuid(), "GEL", 0m, DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow);

        var state = new PosWorkspaceState(scope, session, false, 0, false, false, DateTimeOffset.UtcNow);

        Assert.False(state.CanSell);
    }

    [Fact]
    public async Task SynchronizeOrdersPendingSalesBeforeAuthoritativeSessionAndCatalogRefresh()
    {
        var calls = new List<string>();
        var setup = await Setup(calls: calls);
        await setup.Workspace.OpenOnlineAsync(default);
        await setup.Workspace.CompleteCashSaleAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, 20m,
            [new(setup.ProductId, 1m)]), default);
        calls.Clear();

        await setup.Workspace.SynchronizeAsync(default);

        Assert.Equal(["pending", "session", "catalog"], calls);
    }

    [Fact]
    public async Task PendingSaleReservationStillBlocksCatalogReplacement()
    {
        var setup = await Setup();
        await setup.Workspace.OpenOnlineAsync(default);
        await setup.Workspace.CompleteCashSaleAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow, 20m,
            [new(setup.ProductId, 1m)]), default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqliteSellableCatalog(setup.Path).ApplyAsync(new(setup.Scope.OrganizationId,
                setup.Scope.BranchId, DateTimeOffset.UtcNow.AddMinutes(1), []), default));

        Assert.Equal("Pending sales must be reconciled before replacing stock.", exception.Message);
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

    private async Task<SetupResult> Setup(bool hasCashSession = true, List<string>? calls = null)
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var product = Guid.NewGuid();
        var scope = new PosWorkspaceScope(organization, branch, device);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db");
        var openedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var sessions = new SessionSource(new(new(device, branch, register, "active", 1),
            hasCashSession ? new(shift, branch, register, "open", "GEL", 0m, openedAt) : null,
            DateTimeOffset.UtcNow), calls);
        var item = new LocalSellableItem(organization, branch, product, Guid.NewGuid(), "SKU-1", "Tea", "EA",
            "10001", 2m, 10m, "GEL", "inclusive", 18m, openedAt, null);
        var remoteCatalog = new CatalogSource(new(organization, branch, DateTimeOffset.UtcNow, [item]), calls);
        var transport = new Transport(calls); var cash = new CashManagement(register, openedAt);
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

    private sealed class SessionSource(RemoteCashSessionSnapshot snapshot, List<string>? calls = null)
        : IRemoteCashSessionSource
    {
        public Task<RemoteCashSessionSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
            Guid deviceId, CancellationToken cancellationToken)
        {
            calls?.Add("session");
            return Task.FromResult(snapshot);
        }
    }
    private sealed class CatalogSource(SellableCatalogSnapshot snapshot, List<string>? calls = null)
        : IRemoteSellableCatalog
    {
        public Task<SellableCatalogSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken)
        {
            calls?.Add("catalog");
            return Task.FromResult(snapshot);
        }
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
    private sealed class Transport(List<string>? calls = null) : IRemoteSyncTransport
    {
        public int Sent { get; private set; }
        public Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
            LocalOutboxMessage message, CancellationToken cancellationToken)
        {
            calls?.Add("pending");
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
