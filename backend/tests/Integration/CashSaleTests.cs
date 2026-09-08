using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class CashSaleTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    [Fact]
    public async Task CompletionPersistsFinancialSnapshotAndDecrementsStockOnce()
    {
        var productId = await PrepareProduct(10m, 5m);
        var operationId = Guid.NewGuid();
        using var created = await Complete(productId, 2m, 25m, operationId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(20m, body.RootElement.GetProperty("grandTotal").GetDecimal());
        Assert.Equal(5m, body.RootElement.GetProperty("changeDue").GetDecimal());
        using var replay = await Complete(productId, 2m, 25m, operationId);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var client = Client("owner");
        using var stock = await client.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/inventory/stock");
        using var stockBody = JsonDocument.Parse(await stock.Content.ReadAsStringAsync());
        var item = stockBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("productId").GetGuid() == productId);
        Assert.Equal(3m, item.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task ChangedReplayInsufficientStockAndUnauthorizedCompletionAreRejected()
    {
        var productId = await PrepareProduct(4m, 1m);
        var operationId = Guid.NewGuid();
        using var created = await Complete(productId, 1m, 5m, operationId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var changed = await Complete(productId, 1m, 6m, operationId);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var insufficient = await Complete(productId, 1m, 5m, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        using var denied = await Complete(productId, 1m, 5m, Guid.NewGuid(), "alice");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ConcurrentSalesCannotOversellTheSameStock()
    {
        var productId = await PrepareProduct(3m, 1m);
        var attempts = await Task.WhenAll(
            Complete(productId, 1m, 3m, Guid.NewGuid()),
            Complete(productId, 1m, 3m, Guid.NewGuid()));
        try
        {
            Assert.Equal(1, attempts.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, attempts.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in attempts) response.Dispose();
        }
    }

    private async Task<Guid> PrepareProduct(decimal amount, decimal stock)
    {
        using var client = Client("owner");
        using var productRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/products")
        { Content = JsonContent.Create(new { sku = "SALE." + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = "Sale Item", unitCode = "EA", barcode = (string?)null }) };
        productRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var productResponse = await client.SendAsync(productRequest); productResponse.EnsureSuccessStatusCode();
        using var productBody = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        var productId = productBody.RootElement.GetProperty("id").GetGuid();
        using var priceRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/pricing/prices")
        { Content = JsonContent.Create(new { productId, branchId = fixture.BranchA, amount, currency = "GEL", taxMode = "inclusive", taxRate = 0m, validFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), validUntil = (DateTimeOffset?)null }) };
        priceRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var priceResponse = await client.SendAsync(priceRequest); priceResponse.EnsureSuccessStatusCode();
        using var stockRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/inventory/movements")
        { Content = JsonContent.Create(new { productId, kind = "receipt", quantity = stock, reason = "Sale test stock", occurredAt = DateTimeOffset.UtcNow }) };
        stockRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var stockResponse = await client.SendAsync(stockRequest); stockResponse.EnsureSuccessStatusCode();
        return productId;
    }

    private async Task<HttpResponseMessage> Complete(Guid productId, decimal quantity, decimal cashReceived,
        Guid operationId, string subject = "owner")
    {
        using var client = Client(subject);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/cash")
        { Content = JsonContent.Create(new { lines = new[] { new { productId, quantity } }, cashReceived }) };
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request);
    }

    private HttpClient Client(string subject)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        return client;
    }
}
