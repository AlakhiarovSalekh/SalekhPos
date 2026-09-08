using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class PricingTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    [Fact]
    public async Task BranchPriceOverridesBasePriceAndReplayIsSafe()
    {
        var productId = await CreateProduct();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var operationId = Guid.NewGuid();
        using var created = await Schedule(productId, null, 10m, operationId, start);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var replay = await Schedule(productId, null, 10m, operationId, start);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var branch = await Schedule(productId, fixture.BranchA, 8.5m, Guid.NewGuid(), start);
        Assert.Equal(HttpStatusCode.Created, branch.StatusCode);
        using var client = Client("owner");
        var at = Uri.EscapeDataString(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"));
        using var resolved = await client.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/pricing/resolve?branchId={fixture.BranchA}&productId={productId}&at={at}");
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using var json = JsonDocument.Parse(await resolved.Content.ReadAsStringAsync());
        Assert.Equal(8.5m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(fixture.BranchA, json.RootElement.GetProperty("branchId").GetGuid());
    }

    [Fact]
    public async Task OverlapChangedReplayAndUnauthorizedReadAreDenied()
    {
        var productId = await CreateProduct();
        var start = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var operationId = Guid.NewGuid();
        using var original = await Schedule(productId, null, 11m, operationId, start);
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        using var changed = await Schedule(productId, null, 12m, operationId, start);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var overlap = await Schedule(productId, null, 13m, Guid.NewGuid(), start.AddDays(1));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        using var denied = Client("alice");
        var at = Uri.EscapeDataString(start.AddDays(2).ToString("O"));
        using var response = await denied.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/pricing/resolve?branchId={fixture.BranchA}&productId={productId}&at={at}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient Client(string subject)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        return client;
    }

    private async Task<Guid> CreateProduct()
    {
        using var client = Client("owner");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/products")
        {
            Content = JsonContent.Create(new { sku = "PR." + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = "Priced Item", unitCode = "EA", barcode = (string?)null })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> Schedule(Guid productId, Guid? branchId, decimal amount,
        Guid operationId, DateTimeOffset validFrom)
    {
        using var client = Client("owner");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/pricing/prices")
        {
            Content = JsonContent.Create(new { productId, branchId, amount, currency = "GEL", taxMode = "inclusive", taxRate = 18m, validFrom, validUntil = (DateTimeOffset?)null })
        };
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request);
    }
}
