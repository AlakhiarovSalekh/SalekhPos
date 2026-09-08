using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class InventoryLedgerTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private string Path => $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/inventory";
    [Fact]
    public async Task ImmutableMovementsProduceCurrentStockAndReplaySafely()
    {
        var product = await CreateProduct();
        var operation = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        using var receipt = await Movement(product, operation, "receipt", 10m, occurredAt);
        Assert.Equal(HttpStatusCode.Created, receipt.StatusCode);
        using var replay = await Movement(product, operation, "receipt", 10m, occurredAt);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var adjustment = await Movement(product, Guid.NewGuid(), "adjustment_out", 2.5m);
        Assert.Equal(HttpStatusCode.Created, adjustment.StatusCode);
        using var client = Client("owner");
        using var stock = await client.GetAsync(Path + "/stock");
        Assert.Equal(HttpStatusCode.OK, stock.StatusCode);
        using var json = JsonDocument.Parse(await stock.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("productId").GetGuid() == product);
        Assert.Equal(7.5m, item.GetProperty("quantity").GetDecimal());
    }
    [Fact]
    public async Task ChangedReplayAndUnauthorizedAccessAreDenied()
    {
        var product = await CreateProduct(); var operation = Guid.NewGuid();
        using var original = await Movement(product, operation, "receipt", 3m);
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        using var conflict = await Movement(product, operation, "receipt", 4m);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var denied = Client("alice");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(Path + "/stock")).StatusCode);
    }
    private HttpClient Client(string subject) { var client = fixture.Factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject)); return client; }
    private async Task<Guid> CreateProduct()
    {
        using var client = Client("owner"); using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/products")
        { Content = JsonContent.Create(new { sku = "INV." + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = "Inventory Item", unitCode = "EA", barcode = (string?)null }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("id").GetGuid();
    }
    private async Task<HttpResponseMessage> Movement(Guid product, Guid operation, string kind, decimal quantity, DateTimeOffset? occurredAt = null)
    {
        using var client = Client("owner"); using var request = new HttpRequestMessage(HttpMethod.Post, Path + "/movements")
        { Content = JsonContent.Create(new { productId = product, kind, quantity, reason = "Verified count", occurredAt = occurredAt ?? DateTimeOffset.UtcNow }) };
        request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request);
    }
}
