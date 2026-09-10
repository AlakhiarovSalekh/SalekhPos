using System.Net;
using System.Text;
using System.Text.Json;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class SellableCatalogTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "salekhpos-catalog-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SnapshotIsAtomicDurableScopedAndReplaySafe()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var item = Item(organizationId, branchId);
        var capturedAt = DateTimeOffset.UtcNow; var path = Path.Combine(directory, "catalog.db");
        var catalog = new SqliteSellableCatalog(path);

        Assert.True(await catalog.ApplyAsync(new(organizationId, branchId, capturedAt, [item]), default));
        Assert.False(await catalog.ApplyAsync(new(organizationId, branchId, capturedAt, [item]), default));
        var reopened = new SqliteSellableCatalog(path);
        Assert.Equal(item.ProductId, (await reopened.FindByBarcodeAsync(organizationId, branchId,
            item.Barcode!, capturedAt, default))!.ProductId);
        Assert.Null(await reopened.FindByProductAsync(organizationId, Guid.NewGuid(), item.ProductId, capturedAt, default));

        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ApplyAsync(new(organizationId,
            branchId, capturedAt.AddMinutes(-1), [item]), default));
        Assert.NotNull(await reopened.FindByProductAsync(organizationId, branchId, item.ProductId, capturedAt, default));
    }

    [Fact]
    public async Task InvalidReplacementCannotEraseExistingSnapshot()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var item = Item(organizationId, branchId);
        var catalog = new SqliteSellableCatalog(Path.Combine(directory, "atomic.db")); var now = DateTimeOffset.UtcNow;
        await catalog.ApplyAsync(new(organizationId, branchId, now, [item]), default);

        await Assert.ThrowsAsync<ArgumentException>(() => catalog.ApplyAsync(new(organizationId, branchId,
            now.AddMinutes(1), [item, item]), default));

        Assert.NotNull(await catalog.FindByProductAsync(organizationId, branchId, item.ProductId, now, default));
    }

    [Fact]
    public async Task RemoteDownloadJoinsCatalogStockAndResolvedPriceAtOneInstant()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var productId = Guid.NewGuid();
        var priceId = Guid.NewGuid(); var requests = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            requests.Add(request.RequestUri!.PathAndQuery);
            object body = request.RequestUri.AbsolutePath switch
            {
                var path when path.EndsWith("/products", StringComparison.Ordinal) => new
                {
                    items = new[] { new { id = productId, sku = "SKU-1", name = "Tea", unitCode = "EA", barcode = "12345", isActive = true, version = 1 } },
                    nextCursor = (Guid?)null,
                },
                var path when path.EndsWith("/stock", StringComparison.Ordinal) => new
                {
                    items = new[] { new { productId, sku = "SKU-1", name = "Tea", quantity = 7m } },
                    nextCursor = (Guid?)null,
                },
                _ => new
                {
                    priceId,
                    productId,
                    branchId = (Guid?)branchId,
                    amount = 3.5m,
                    currency = "GEL",
                    taxMode = "inclusive",
                    taxRate = 18m,
                    validFrom = DateTimeOffset.UtcNow.AddDays(-1),
                    validUntil = (DateTimeOffset?)null
                },
            };
            return Json(body);
        }))
        { BaseAddress = new Uri("https://pos.test/") };

        var snapshot = await new HttpRemoteSellableCatalog(client).DownloadAsync(organizationId, branchId, default);

        var item = Assert.Single(snapshot.Items); Assert.Equal(productId, item.ProductId); Assert.Equal(7m, item.StockQuantity);
        Assert.Equal(3, requests.Count); Assert.Contains(requests, x => x.Contains("/pricing/resolve?", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InconsistentRemoteEvidenceIsRejectedBeforeLocalReplacement()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var productId = Guid.NewGuid();
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/products", StringComparison.Ordinal)
            ? Json(new { items = new[] { new { id = productId, sku = "A", name = "Tea", unitCode = "EA", barcode = (string?)null, isActive = true, version = 1 } }, nextCursor = (Guid?)null })
            : Json(new { items = new[] { new { productId, sku = "CHANGED", name = "Tea", quantity = 1m } }, nextCursor = (Guid?)null })))
        { BaseAddress = new Uri("https://pos.test/") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HttpRemoteSellableCatalog(client).DownloadAsync(organizationId, branchId, default));
    }

    private static LocalSellableItem Item(Guid organizationId, Guid branchId) => new(organizationId, branchId,
        Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Tea", "EA", "12345", 8m, 3.5m, "GEL", "inclusive",
        18m, DateTimeOffset.UtcNow.AddDays(-1), null);
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"),
    };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
