using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Domain.Shifts;

namespace SalekhPos.Desktop.Application.POS;

public sealed record PosWorkspaceScope(Guid OrganizationId, Guid BranchId, Guid DeviceId);
public sealed class CatalogProjectionNotReadyException()
    : InvalidOperationException("Verified offline catalog data is not available for this organization and branch.");
public sealed record PosWorkspaceState(PosWorkspaceScope Scope, LocalCashSession? CashSession,
    bool IsCatalogProjectionReady, int PendingSales, bool PendingSalesTruncated, bool IsOnline,
    DateTimeOffset ObservedAt)
{
    public bool CanSell => CashSession is not null && IsCatalogProjectionReady;
}
public sealed record CashCheckoutRequest(Guid SaleId, DateTimeOffset CompletedAt, decimal CashReceived,
    IReadOnlyList<ProjectedSaleItem> Items);

public interface IPosWorkspace
{
    PosWorkspaceState CurrentState { get; }
    Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken);
    Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken);
    Task<LocalSellableItem?> FindByProductAsync(Guid productId, DateTimeOffset at, CancellationToken cancellationToken);
    Task<LocalSellableItem?> FindByBarcodeAsync(string barcode, DateTimeOffset at, CancellationToken cancellationToken);
    Task<LocalSaleWriteResult> CompleteCashSaleAsync(CashCheckoutRequest request, CancellationToken cancellationToken);
    Task<PosWorkspaceState> SynchronizeAsync(CancellationToken cancellationToken);
    Task<OpenCashSessionResult> OpenCashSessionAsync(Guid operationId, string currency, decimal openingBalance,
        CancellationToken cancellationToken);
    Task<CashMovementResult> RecordCashMovementAsync(Guid operationId, string kind, decimal amount, string reason,
        CancellationToken cancellationToken);
    Task<ClosedCashSessionResult> CloseCashSessionAsync(Guid operationId, decimal countedCash,
        CancellationToken cancellationToken);
}

public sealed class PosWorkspace(
    PosWorkspaceScope scope,
    CashSessionCoordinator cashSessions,
    ILocalCashSessionStore localCashSessions,
    SellableCatalogRefresh catalogRefresh,
    ILocalSellableCatalog catalog,
    IProjectedSaleCheckout checkout,
    PendingSaleSyncRunner sync,
    ILocalSaleStore sales,
    IRemoteCashManagement cashManagement,
    ITrustedDeviceRegisterAssignmentReader deviceAssignments) : IPosWorkspace
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private PosWorkspaceState? state;
    public PosWorkspaceState CurrentState => state
        ?? throw new InvalidOperationException("The POS workspace is not open.");

    public async Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken)
    {
        ValidateScope();
        await gate.WaitAsync(cancellationToken);
        try
        {
            await sync.RunAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId, 100, 4,
                TimeSpan.FromMilliseconds(250), cancellationToken);
            var session = await cashSessions.RefreshAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                cancellationToken);
            await catalogRefresh.RefreshAsync(scope.OrganizationId, scope.BranchId, cancellationToken);
            return state = await Snapshot(session, true, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken)
    {
        ValidateScope();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = await localCashSessions.ReadActiveAsync(scope.OrganizationId, scope.BranchId,
                scope.DeviceId, cancellationToken);
            var offlineState = await Snapshot(session, false, cancellationToken);
            if (!offlineState.IsCatalogProjectionReady)
                throw new CatalogProjectionNotReadyException();
            return state = offlineState;
        }
        finally { gate.Release(); }
    }

    public async Task<LocalSellableItem?> FindByProductAsync(Guid productId, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product identity is required.");
        EnsureOpened();
        return await catalog.FindByProductAsync(scope.OrganizationId, scope.BranchId, productId, at,
            cancellationToken);
    }

    public async Task<LocalSellableItem?> FindByBarcodeAsync(string barcode, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode)) throw new ArgumentException("Barcode is required.");
        EnsureOpened();
        return await catalog.FindByBarcodeAsync(scope.OrganizationId, scope.BranchId, barcode, at,
            cancellationToken);
    }

    public async Task<LocalSaleWriteResult> CompleteCashSaleAsync(CashCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.SaleId == Guid.Empty) throw new ArgumentException("Checkout request is invalid.");
        EnsureOpened();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result = await checkout.CompleteAsync(new(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                request.SaleId, request.CompletedAt, request.CashReceived, request.Items), cancellationToken);
            state = await Snapshot(state!.CashSession, state.IsOnline, cancellationToken);
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<PosWorkspaceState> SynchronizeAsync(CancellationToken cancellationToken)
    {
        EnsureOpened();
        await gate.WaitAsync(cancellationToken);
        try
        {
            await sync.RunAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId, 100, 4,
                TimeSpan.FromMilliseconds(250), cancellationToken);
            var session = await cashSessions.RefreshAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                cancellationToken);
            await catalogRefresh.RefreshAsync(scope.OrganizationId, scope.BranchId, cancellationToken);
            return state = await Snapshot(session, true, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<OpenCashSessionResult> OpenCashSessionAsync(Guid operationId, string currency,
        decimal openingBalance, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty || currency is null || currency.Length != 3
            || currency.Any(c => c is < 'A' or > 'Z') || openingBalance < 0
            || decimal.Round(openingBalance, 6) != openingBalance)
            throw new ArgumentException("Shift-opening request is invalid.");
        EnsureOpened();
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!state!.IsOnline) throw new InvalidOperationException("A cash session can only be opened online.");
            if (!state.IsCatalogProjectionReady)
                throw new InvalidOperationException("The verified catalog must be ready before opening a cash session.");
            if (state.CashSession is not null)
                throw new InvalidOperationException("A cash session is already open.");
            var assignment = await deviceAssignments.ReadAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                cancellationToken);
            if (assignment.OrganizationId != scope.OrganizationId || assignment.BranchId != scope.BranchId
                || assignment.DeviceId != scope.DeviceId || assignment.RegisterId == Guid.Empty)
                throw new InvalidOperationException("The trusted device assignment does not match the workspace.");
            var result = await cashManagement.OpenAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                new(assignment.RegisterId, currency, openingBalance, operationId), cancellationToken);
            if (result.ShiftId == Guid.Empty || result.BranchId != scope.BranchId
                || result.RegisterId != assignment.RegisterId || result.Status != "open"
                || result.Currency != currency || result.OpeningBalance != openingBalance
                || result.OpenedAt == default || result.OpenedAt.Offset != TimeSpan.Zero
                || string.IsNullOrWhiteSpace(result.OpenedBy) || result.OpenedBy != result.OpenedBy.Trim()
                || result.OpenedBy.Length > 512 || result.OpenedBy.Any(char.IsControl))
                throw new InvalidOperationException("The opened shift does not match the requested cash session.");
            var refreshed = await cashSessions.RefreshOpenedAsync(scope.OrganizationId, scope.BranchId,
                scope.DeviceId, result, cancellationToken);
            if (refreshed is null || refreshed.OrganizationId != scope.OrganizationId
                || refreshed.BranchId != scope.BranchId || refreshed.DeviceId != scope.DeviceId
                || refreshed.RegisterId != assignment.RegisterId || refreshed.RegisterId != result.RegisterId
                || refreshed.ShiftId != result.ShiftId || refreshed.Currency != result.Currency
                || refreshed.OpeningBalance != result.OpeningBalance || refreshed.OpenedAt != result.OpenedAt)
                throw new InvalidOperationException("The authoritative cash session does not match the opened shift.");
            state = await Snapshot(refreshed, true, cancellationToken);
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<CashMovementResult> RecordCashMovementAsync(Guid operationId, string kind, decimal amount,
        string reason, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty || kind is not ("cash_in" or "cash_out") || amount <= 0
            || decimal.Round(amount, 6) != amount || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cash-movement request is invalid.");
        EnsureOnlineSession();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = await ReadTrustedCurrentSessionAsync(cancellationToken);
            var result = await cashManagement.RecordMovementAsync(scope.OrganizationId, scope.BranchId,
                scope.DeviceId, session.ShiftId, session.RegisterId, operationId, kind, amount, reason,
                cancellationToken);
            if (result.ShiftId != session.ShiftId || result.Kind != kind || result.Currency != session.Currency
                || result.Amount != amount || result.Reason != reason)
                throw new InvalidOperationException("The cash movement does not match the active cash session.");
            state = await Snapshot(session, true, cancellationToken);
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<ClosedCashSessionResult> CloseCashSessionAsync(Guid operationId, decimal countedCash,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty || countedCash < 0 || decimal.Round(countedCash, 6) != countedCash)
            throw new ArgumentException("Shift-closing request is invalid.");
        EnsureOnlineSession();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = await ReadTrustedCurrentSessionAsync(cancellationToken);
            if ((await sales.ReadPendingAsync(scope.DeviceId, 1, cancellationToken)).Count != 0)
                throw new InvalidOperationException("Pending sales must synchronize before closing the cash session.");
            var result = await cashManagement.CloseAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                session.ShiftId, session.RegisterId, operationId, countedCash, cancellationToken);
            if (result.ShiftId != session.ShiftId || result.BranchId != scope.BranchId
                || result.RegisterId != session.RegisterId || result.Currency != session.Currency
                || result.CountedCash != countedCash || result.OpenedAt != session.OpenedAt)
                throw new InvalidOperationException("The closed shift does not match the active cash session.");
            await localCashSessions.ConfirmClosedAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
                session.ShiftId, cancellationToken);
            state = await Snapshot(null, true, cancellationToken);
            return result;
        }
        finally { gate.Release(); }
    }

    private async Task<PosWorkspaceState> Snapshot(LocalCashSession? session, bool online, CancellationToken ct)
    {
        var pending = await sales.ReadPendingAsync(scope.DeviceId, 100, ct);
        var catalogReady = await catalog.IsProjectionReadyAsync(scope.OrganizationId, scope.BranchId, ct);
        return new(scope, session, catalogReady, pending.Count, pending.Count == 100, online,
            DateTimeOffset.UtcNow);
    }
    private void EnsureOpened() { if (state is null) throw new InvalidOperationException("The POS workspace is not open."); }
    private void EnsureOnlineSession()
    {
        EnsureOpened();
        if (!state!.IsOnline || state.CashSession is null)
            throw new InvalidOperationException("An online cash session is required.");
    }
    private async Task<LocalCashSession> ReadTrustedCurrentSessionAsync(CancellationToken cancellationToken)
    {
        if (state is null || !state.IsOnline || state.CashSession is null)
            throw new InvalidOperationException("An online cash session is required.");
        var session = state.CashSession;
        if (session.OrganizationId != scope.OrganizationId || session.BranchId != scope.BranchId
            || session.DeviceId != scope.DeviceId || session.RegisterId == Guid.Empty || session.ShiftId == Guid.Empty)
            throw new InvalidOperationException("The active cash session does not match the workspace.");
        var assignment = await deviceAssignments.ReadAsync(scope.OrganizationId, scope.BranchId, scope.DeviceId,
            cancellationToken);
        if (assignment.OrganizationId != scope.OrganizationId || assignment.BranchId != scope.BranchId
            || assignment.DeviceId != scope.DeviceId || assignment.RegisterId != session.RegisterId)
            throw new InvalidOperationException("The trusted device assignment does not match the active cash session.");
        return session;
    }
    private void ValidateScope() { if (scope.OrganizationId == Guid.Empty || scope.BranchId == Guid.Empty || scope.DeviceId == Guid.Empty) throw new ArgumentException("POS workspace scope is required."); }
}
