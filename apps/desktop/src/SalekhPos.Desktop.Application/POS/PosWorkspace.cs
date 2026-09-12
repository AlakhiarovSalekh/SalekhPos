using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Domain.Shifts;

namespace SalekhPos.Desktop.Application.POS;

public sealed record PosWorkspaceScope(Guid OrganizationId, Guid BranchId, Guid DeviceId);
public sealed record PosWorkspaceState(PosWorkspaceScope Scope, LocalCashSession? CashSession, int PendingSales,
    bool PendingSalesTruncated, bool IsOnline, DateTimeOffset ObservedAt)
{
    public bool CanSell => CashSession is not null;
}
public sealed record CashCheckoutRequest(Guid SaleId, DateTimeOffset CompletedAt, decimal CashReceived,
    IReadOnlyList<ProjectedSaleItem> Items);

public sealed class PosWorkspace(
    PosWorkspaceScope scope,
    CashSessionCoordinator cashSessions,
    ILocalCashSessionStore localCashSessions,
    SellableCatalogRefresh catalogRefresh,
    ILocalSellableCatalog catalog,
    IProjectedSaleCheckout checkout,
    PendingSaleSyncRunner sync,
    ILocalSaleStore sales)
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
            if (session is not null)
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
            return state = await Snapshot(session, false, cancellationToken);
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
            var session = await localCashSessions.ReadActiveAsync(scope.OrganizationId, scope.BranchId,
                scope.DeviceId, cancellationToken);
            return state = await Snapshot(session, true, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<PosWorkspaceState> Snapshot(LocalCashSession? session, bool online, CancellationToken ct)
    {
        var pending = await sales.ReadPendingAsync(scope.DeviceId, 100, ct);
        return new(scope, session, pending.Count, pending.Count == 100, online, DateTimeOffset.UtcNow);
    }
    private void EnsureOpened() { if (state is null) throw new InvalidOperationException("The POS workspace is not open."); }
    private void ValidateScope() { if (scope.OrganizationId == Guid.Empty || scope.BranchId == Guid.Empty || scope.DeviceId == Guid.Empty) throw new ArgumentException("POS workspace scope is required."); }
}
