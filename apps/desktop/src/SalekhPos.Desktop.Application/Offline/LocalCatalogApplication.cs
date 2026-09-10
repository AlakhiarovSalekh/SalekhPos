using SalekhPos.Desktop.Domain.LocalCatalog;

namespace SalekhPos.Desktop.Application.Offline;

public sealed record SellableCatalogSnapshot(Guid OrganizationId, Guid BranchId, DateTimeOffset CapturedAt,
    IReadOnlyList<LocalSellableItem> Items);

public interface ILocalSellableCatalog
{
    Task<bool> ApplyAsync(SellableCatalogSnapshot snapshot, CancellationToken cancellationToken);
    Task<LocalSellableItem?> FindByProductAsync(Guid organizationId, Guid branchId, Guid productId,
        DateTimeOffset at, CancellationToken cancellationToken);
    Task<LocalSellableItem?> FindByBarcodeAsync(Guid organizationId, Guid branchId, string barcode,
        DateTimeOffset at, CancellationToken cancellationToken);
}

public interface IRemoteSellableCatalog
{
    Task<SellableCatalogSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
        CancellationToken cancellationToken);
}

public sealed class SellableCatalogRefresh(IRemoteSellableCatalog remote, ILocalSellableCatalog local)
{
    public async Task<bool> RefreshAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken) =>
        await local.ApplyAsync(await remote.DownloadAsync(organizationId, branchId, cancellationToken), cancellationToken);
}
