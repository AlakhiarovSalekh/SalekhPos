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
        var saleId = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(20m, body.RootElement.GetProperty("grandTotal").GetDecimal());
        Assert.Equal(5m, body.RootElement.GetProperty("changeDue").GetDecimal());
        using var reader = Client("owner");
        using var read = await reader.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{saleId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var readBody = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(saleId, readBody.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(20m, readBody.RootElement.GetProperty("grandTotal").GetDecimal());
        Assert.Single(readBody.RootElement.GetProperty("lines").EnumerateArray());
        using var receipt = await reader.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{saleId}/receipt");
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        using var receiptBody = JsonDocument.Parse(await receipt.Content.ReadAsStringAsync());
        Assert.Equal(saleId.ToString("N").ToUpperInvariant(), receiptBody.RootElement.GetProperty("receiptNumber").GetString());
        Assert.Equal(20m, receiptBody.RootElement.GetProperty("grandTotal").GetDecimal());
        Assert.Equal(5m, receiptBody.RootElement.GetProperty("changeDue").GetDecimal());
        Assert.Single(receiptBody.RootElement.GetProperty("lines").EnumerateArray());
        using var payment = await reader.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{saleId}/payment");
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        using var paymentBody = JsonDocument.Parse(await payment.Content.ReadAsStringAsync());
        Assert.Equal(saleId, paymentBody.RootElement.GetProperty("saleId").GetGuid());
        Assert.Equal("cash", paymentBody.RootElement.GetProperty("method").GetString());
        Assert.Equal("completed", paymentBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(20m, paymentBody.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(25m, paymentBody.RootElement.GetProperty("tendered").GetDecimal());
        using var list = await reader.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(listBody.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == saleId);
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
    public async Task SaleReadRequiresPermissionAndReturnsNotFoundWithinAuthorizedScope()
    {
        using var owner = Client("owner");
        using var missing = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var alice = Client("alice");
        using var denied = await alice.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var paymentDenied = await alice.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales/{Guid.NewGuid()}/payment");
        Assert.Equal(HttpStatusCode.Forbidden, paymentDenied.StatusCode);
        using var invalid = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
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

    [Fact]
    public async Task SaleHistoryUsesStableBoundedKeysetPages()
    {
        var productId = await PrepareProduct(2m, 2m);
        using var firstSale = await Complete(productId, 1m, 2m, Guid.NewGuid());
        using var secondSale = await Complete(productId, 1m, 2m, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, firstSale.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondSale.StatusCode);

        using var client = Client("owner");
        using var firstPage = await client.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales?pageSize=1");
        using var firstBody = JsonDocument.Parse(await firstPage.Content.ReadAsStringAsync());
        var firstItem = Assert.Single(firstBody.RootElement.GetProperty("items").EnumerateArray());
        var cursor = firstBody.RootElement.GetProperty("nextCursor").GetGuid();
        Assert.Equal(firstItem.GetProperty("id").GetGuid(), cursor);

        using var secondPage = await client.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/sales?pageSize=1&after={cursor:D}");
        using var secondBody = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());
        var secondItem = Assert.Single(secondBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.NotEqual(firstItem.GetProperty("id").GetGuid(), secondItem.GetProperty("id").GetGuid());
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
