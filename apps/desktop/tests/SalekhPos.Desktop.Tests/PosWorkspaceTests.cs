using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.Devices;
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
        var (movementDeviceId, movementRegisterId, _) = Assert.Single(setup.Cash.MovementRequests);
        Assert.Equal(setup.Scope.DeviceId, movementDeviceId);
        Assert.Equal(setup.RegisterId, movementRegisterId);
        var (closeDeviceId, closeRegisterId, _) = Assert.Single(setup.Cash.CloseRequests);
        Assert.Equal(setup.Scope.DeviceId, closeDeviceId);
        Assert.Equal(setup.RegisterId, closeRegisterId);
        Assert.Null(await new SqliteCashSessionStore(setup.Path).ReadActiveAsync(setup.Scope.OrganizationId,
            setup.Scope.BranchId, setup.Scope.DeviceId, default));
    }

    [Fact]
    public async Task CashWritesFailClosedForMissingOrMismatchedSessionAssignmentEvidence()
    {
        var missing = await Setup(hasCashSession: false); await missing.Workspace.OpenOnlineAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.Workspace.RecordCashMovementAsync(
            Guid.NewGuid(), "cash_in", 1m, "Float", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.Workspace.CloseCashSessionAsync(
            Guid.NewGuid(), 0m, default));
        Assert.Empty(missing.Cash.MovementRequests); Assert.Empty(missing.Cash.CloseRequests);

        var mismatched = await Setup(); await mismatched.Workspace.OpenOnlineAsync(default);
        mismatched.Assignment.RegisterId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => mismatched.Workspace.RecordCashMovementAsync(
            Guid.NewGuid(), "cash_out", 1m, "Petty cash", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => mismatched.Workspace.CloseCashSessionAsync(
            Guid.NewGuid(), 0m, default));
        Assert.Empty(mismatched.Cash.MovementRequests); Assert.Empty(mismatched.Cash.CloseRequests);
    }

    [Fact]
    public async Task ShiftOpenUsesTrustedAssignmentAndPublishesOnlyMatchingAuthoritativeRefresh()
    {
        var setup = await Setup(hasCashSession: false); await setup.Workspace.OpenOnlineAsync(default);
        var operation = Guid.NewGuid();

        var result = await setup.Workspace.OpenCashSessionAsync(operation, "GEL", 7.123456m, default);

        Assert.Equal(setup.RegisterId, result.RegisterId);
        Assert.Equal((setup.Scope.OrganizationId, setup.Scope.BranchId, setup.Scope.DeviceId),
            setup.Assignment.RequestedScope);
        var request = Assert.Single(setup.Cash.OpenRequests);
        Assert.Equal(operation, request.OperationId); Assert.Equal(setup.RegisterId, request.RegisterId);
        Assert.True(setup.Workspace.CurrentState.CanSell);
        Assert.Equal(result.ShiftId, setup.Workspace.CurrentState.CashSession!.ShiftId);
    }

    [Fact]
    public async Task ShiftOpenFailsClosedForOfflineExistingSessionOrMissingAssignment()
    {
        var offline = await Setup(hasCashSession: false); await offline.Workspace.OpenOnlineAsync(default);
        await offline.Workspace.OpenOfflineAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => offline.Workspace.OpenCashSessionAsync(
            Guid.NewGuid(), "GEL", 0m, default));
        Assert.Empty(offline.Cash.OpenRequests);

        var existing = await Setup(); await existing.Workspace.OpenOnlineAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => existing.Workspace.OpenCashSessionAsync(
            Guid.NewGuid(), "GEL", 0m, default));
        Assert.Empty(existing.Cash.OpenRequests);

        var missing = await Setup(hasCashSession: false); await missing.Workspace.OpenOnlineAsync(default);
        missing.Assignment.Failure = new InvalidOperationException("missing assignment");
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.Workspace.OpenCashSessionAsync(
            Guid.NewGuid(), "GEL", 0m, default));
        Assert.Empty(missing.Cash.OpenRequests); Assert.False(missing.Workspace.CurrentState.CanSell);
    }

    [Fact]
    public async Task ShiftOpenRequiresPreparedCatalogReadiness()
    {
        var setup = await Setup(hasCashSession: false, catalogReady: false);
        var state = await setup.Workspace.OpenOnlineAsync(default);

        Assert.False(state.IsCatalogProjectionReady);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Workspace.OpenCashSessionAsync(
            Guid.NewGuid(), "GEL", 0m, default));
        Assert.Empty(setup.Cash.OpenRequests);
    }

    [Fact]
    public async Task ShiftOpenRefreshMismatchNeverEnablesSelling()
    {
        var setup = await Setup(hasCashSession: false, mismatchedOpenRefresh: true);
        await setup.Workspace.OpenOnlineAsync(default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Workspace.OpenCashSessionAsync(
            Guid.NewGuid(), "GEL", 3m, default));

        Assert.Single(setup.Cash.OpenRequests); Assert.False(setup.Workspace.CurrentState.CanSell);
        Assert.Null(setup.Workspace.CurrentState.CashSession);
        Assert.Null(await new SqliteCashSessionStore(setup.Path).ReadActiveAsync(setup.Scope.OrganizationId,
            setup.Scope.BranchId, setup.Scope.DeviceId, default));
    }

    private async Task<SetupResult> Setup(bool hasCashSession = true, List<string>? calls = null,
        bool mismatchedOpenRefresh = false, bool catalogReady = true)
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
        var transport = new Transport(calls); var assignment = new Assignment(scope, register);
        var cash = new CashManagement(register, openedAt, shift, result => sessions.SetShift(new(result.ShiftId,
            branch, register, "open", mismatchedOpenRefresh ? "USD" : result.Currency, result.OpeningBalance,
            result.OpenedAt)));
        return new(scope, path, product, shift, register,
            await Workspace(scope, path, sessions, remoteCatalog, transport, cash, register, assignment,
                catalogReady), transport,
            cash, assignment);
    }

    private static async Task<PosWorkspace> Workspace(PosWorkspaceScope scope, string path,
        IRemoteCashSessionSource sessionSource, IRemoteSellableCatalog remoteCatalog, Transport transport,
        IRemoteCashManagement? cashManagement = null, Guid? registerId = null, Assignment? assignment = null,
        bool catalogReady = true)
    {
        var sessionStore = new SqliteCashSessionStore(path); var sqliteCatalog = new SqliteSellableCatalog(path);
        ILocalSellableCatalog catalog = catalogReady ? sqliteCatalog : new NotReadyCatalog(sqliteCatalog);
        var sales = await new SqliteLocalSaleStore(path).OpenAsync();
        return new(scope, new(sessionSource, sessionStore), sessionStore, new(remoteCatalog, catalog), catalog,
            new SqliteProjectedSaleCheckout(path),
            new PendingSaleSyncRunner(new PendingSaleSyncDispatcher(sales, transport), new Delay()), sales,
            cashManagement ?? new CashManagement(Guid.NewGuid(), DateTimeOffset.UtcNow),
            assignment ?? new Assignment(scope, registerId ?? Guid.NewGuid()));
    }

    private sealed class SessionSource(RemoteCashSessionSnapshot snapshot, List<string>? calls = null)
        : IRemoteCashSessionSource
    {
        private RemoteCashSessionSnapshot snapshot = snapshot;
        public void SetShift(RemoteOpenShift shift) => snapshot = snapshot with
        { Shift = shift, CapturedAt = DateTimeOffset.UtcNow };
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
    private sealed class CashManagement(Guid registerId, DateTimeOffset openedAt, Guid? openShiftId = null,
        Action<OpenCashSessionResult>? onOpen = null) : IRemoteCashManagement
    {
        public List<OpenCashSessionRequest> OpenRequests { get; } = [];
        public List<(Guid DeviceId, Guid RegisterId, Guid OperationId)> MovementRequests { get; } = [];
        public List<(Guid DeviceId, Guid RegisterId, Guid OperationId)> CloseRequests { get; } = [];
        public Task<OpenCashSessionResult> OpenAsync(Guid organizationId, Guid branchId, Guid deviceId,
            OpenCashSessionRequest request, CancellationToken cancellationToken)
        {
            OpenRequests.Add(request);
            var result = new OpenCashSessionResult(openShiftId ?? Guid.NewGuid(), branchId, request.RegisterId,
                "open", request.Currency, request.OpeningBalance, openedAt, "cashier");
            onOpen?.Invoke(result);
            return Task.FromResult(result);
        }
        public Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid shiftId, Guid requestedRegisterId, Guid operationId, string kind, decimal amount, string reason,
            CancellationToken cancellationToken)
        {
            MovementRequests.Add((deviceId, requestedRegisterId, operationId));
            return Task.FromResult(new CashMovementResult(Guid.NewGuid(), shiftId, kind, "GEL", amount, reason,
                DateTimeOffset.UtcNow, "cashier"));
        }
        public Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid shiftId, Guid requestedRegisterId, Guid operationId, decimal countedCash,
            CancellationToken cancellationToken)
        {
            CloseRequests.Add((deviceId, requestedRegisterId, operationId));
            return Task.FromResult(new ClosedCashSessionResult(shiftId, branchId, registerId, "GEL", 0m, 0m, 0m,
                5m, 0m, 5m, countedCash, countedCash - 5m, openedAt, DateTimeOffset.UtcNow, "cashier", "cashier"));
        }
    }
    private sealed class Assignment(PosWorkspaceScope scope, Guid registerId)
        : ITrustedDeviceRegisterAssignmentReader
    {
        public Exception? Failure { get; set; }
        public Guid RegisterId { get; set; } = registerId;
        public (Guid OrganizationId, Guid BranchId, Guid DeviceId) RequestedScope { get; private set; }
        public Task<TrustedDeviceRegisterAssignment> ReadAsync(Guid organizationId, Guid branchId, Guid deviceId,
            CancellationToken cancellationToken)
        {
            RequestedScope = (organizationId, branchId, deviceId);
            return Failure is null ? Task.FromResult(new TrustedDeviceRegisterAssignment(
                scope.OrganizationId, scope.BranchId, scope.DeviceId, RegisterId))
                : Task.FromException<TrustedDeviceRegisterAssignment>(Failure);
        }
    }
    private sealed class NotReadyCatalog(ILocalSellableCatalog inner) : ILocalSellableCatalog
    {
        public Task<bool> ApplyAsync(SellableCatalogSnapshot snapshot, CancellationToken cancellationToken) =>
            inner.ApplyAsync(snapshot, cancellationToken);
        public Task<bool> IsProjectionReadyAsync(Guid organizationId, Guid branchId,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<LocalSellableItem?> FindByProductAsync(Guid organizationId, Guid branchId, Guid productId,
            DateTimeOffset at, CancellationToken cancellationToken) =>
            inner.FindByProductAsync(organizationId, branchId, productId, at, cancellationToken);
        public Task<LocalSellableItem?> FindByBarcodeAsync(Guid organizationId, Guid branchId, string barcode,
            DateTimeOffset at, CancellationToken cancellationToken) =>
            inner.FindByBarcodeAsync(organizationId, branchId, barcode, at, cancellationToken);
    }
    private sealed record SetupResult(PosWorkspaceScope Scope, string Path, Guid ProductId, Guid ShiftId,
        Guid RegisterId, PosWorkspace Workspace, Transport Transport, CashManagement Cash, Assignment Assignment);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
