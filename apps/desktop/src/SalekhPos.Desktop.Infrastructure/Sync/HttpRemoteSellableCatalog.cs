using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalCatalog;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteSellableCatalog(HttpClient client) : IRemoteSellableCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SellableCatalogSnapshot> DownloadAsync(Guid organizationId, Guid branchId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Catalog scope is required.");

        var capturedAt = DateTimeOffset.UtcNow;
        var products = await Products(organizationId, cancellationToken);
        var stock = await Stock(organizationId, branchId, cancellationToken);
        if (stock.Keys.Except(products.Keys).Any())
            throw new InvalidOperationException("Inventory contains an unknown catalog product.");

        var items = new List<LocalSellableItem>();
        foreach (var product in products.Values.Where(x => x.IsActive).OrderBy(x => x.Id))
        {
            if (!stock.TryGetValue(product.Id, out var level))
                throw new InvalidOperationException("The inventory snapshot is incomplete.");
            if (level.Sku != product.Sku || level.Name != product.Name)
                throw new InvalidOperationException("Catalog and inventory evidence disagree.");

            var path = $"api/v1/organizations/{organizationId:D}/pricing/resolve?branchId={branchId:D}&productId={product.Id:D}&at={Uri.EscapeDataString(capturedAt.ToString("O", CultureInfo.InvariantCulture))}";
            using var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            response.EnsureSuccessStatusCode();
            var price = await response.Content.ReadFromJsonAsync<Price>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("The resolved price body is empty.");
            if (price.ProductId != product.Id || price.BranchId is not null && price.BranchId != branchId)
                throw new InvalidOperationException("The resolved price scope is invalid.");
            items.Add(new(organizationId, branchId, product.Id, price.PriceId, product.Sku, product.Name,
                product.UnitCode, product.Barcode, level.Quantity, price.Amount, price.Currency, price.TaxMode,
                price.TaxRate, price.ValidFrom, price.ValidUntil));
        }
        return new(organizationId, branchId, capturedAt, items.AsReadOnly());
    }

    private async Task<Dictionary<Guid, Product>> Products(Guid organizationId, CancellationToken ct)
    {
        var results = new Dictionary<Guid, Product>(); Guid? after = null;
        for (var pageNumber = 0; pageNumber < 1_000; pageNumber++)
        {
            var path = $"api/v1/organizations/{organizationId:D}/products?pageSize=100"
                + (after is null ? "" : $"&after={after:D}");
            var page = await client.GetFromJsonAsync<Page<Product>>(path, JsonOptions, ct)
                ?? throw new InvalidOperationException("The product page is empty.");
            Add(results, page.Items, x => x.Id, "product");
            if (page.NextCursor is null) return results;
            if (page.NextCursor == after) throw new InvalidOperationException("The product cursor did not advance.");
            after = page.NextCursor;
        }
        throw new InvalidOperationException("The product snapshot exceeded the page limit.");
    }

    private async Task<Dictionary<Guid, StockLevel>> Stock(Guid organizationId, Guid branchId, CancellationToken ct)
    {
        var results = new Dictionary<Guid, StockLevel>(); Guid? after = null;
        for (var pageNumber = 0; pageNumber < 1_000; pageNumber++)
        {
            var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/inventory/stock?pageSize=100"
                + (after is null ? "" : $"&after={after:D}");
            var page = await client.GetFromJsonAsync<Page<StockLevel>>(path, JsonOptions, ct)
                ?? throw new InvalidOperationException("The stock page is empty.");
            Add(results, page.Items, x => x.ProductId, "stock");
            if (page.NextCursor is null) return results;
            if (page.NextCursor == after) throw new InvalidOperationException("The stock cursor did not advance.");
            after = page.NextCursor;
        }
        throw new InvalidOperationException("The stock snapshot exceeded the page limit.");
    }

    private static void Add<T>(Dictionary<Guid, T> target, IReadOnlyList<T> items, Func<T, Guid> key, string kind)
    {
        if (items.Count > 100) throw new InvalidOperationException($"The {kind} page exceeds its contract.");
        foreach (var item in items)
            if (!target.TryAdd(key(item), item)) throw new InvalidOperationException($"Duplicate {kind} evidence.");
    }

    private sealed record Page<T>(IReadOnlyList<T> Items, Guid? NextCursor);
    private sealed record Product(Guid Id, string Sku, string Name, string UnitCode, string? Barcode,
        bool IsActive, long Version);
    private sealed record StockLevel(Guid ProductId, string Sku, string Name, decimal Quantity);
    private sealed record Price(Guid PriceId, Guid ProductId, Guid? BranchId, decimal Amount, string Currency,
        string TaxMode, decimal TaxRate, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
}
