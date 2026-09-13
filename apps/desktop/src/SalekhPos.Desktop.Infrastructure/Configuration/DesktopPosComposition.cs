using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using SalekhPos.Desktop.Infrastructure.Devices;
using SalekhPos.Desktop.Infrastructure.Sync;

namespace SalekhPos.Desktop.Infrastructure.Configuration;

public static class DesktopPosComposition
{
    public static async Task<PosWorkspace> CreateAsync(PosWorkspaceScope scope, string databasePath,
        HttpClient authenticatedClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authenticatedClient);
        var cashSessionStore = new SqliteCashSessionStore(databasePath);
        var saleStore = await new SqliteLocalSaleStore(databasePath).OpenAsync(cancellationToken);
        var proofMaterial = new SqliteDeviceProvisioningStateStore(databasePath);
        var proofSigner = new DeviceRequestProofSigner(DeviceSigningKeyProvider.CreateForCurrentPlatform(),
            proofMaterial);
        return new(scope,
            new CashSessionCoordinator(new HttpRemoteCashSessionSource(authenticatedClient), cashSessionStore),
            cashSessionStore,
            new SellableCatalogRefresh(new HttpRemoteSellableCatalog(authenticatedClient),
                new SqliteSellableCatalog(databasePath)),
            new SqliteSellableCatalog(databasePath),
            new SqliteProjectedSaleCheckout(databasePath),
            new PendingSaleSyncRunner(new PendingSaleSyncDispatcher(saleStore,
                new HttpRemoteSyncTransport(authenticatedClient, proofSigner)), new SystemSyncRetryDelay()),
            saleStore, new HttpRemoteCashManagement(authenticatedClient, proofSigner), proofMaterial);
    }

    private sealed class SystemSyncRetryDelay : ISyncRetryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);
    }
}
