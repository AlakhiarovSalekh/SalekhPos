using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class ProductCatalogTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private string Path(Guid? organization = null) => $"/api/v1/organizations/{organization ?? fixture.OrganizationA}/products";

    [Fact]
    public async Task AuthorizedCreateIsIdempotentAuditedAndReadable()
    {
        var operation = Guid.NewGuid();
        var first = await SendCreate(operation, "MILK.001", "Milk 1 L");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var productId = firstJson.RootElement.GetProperty("id").GetGuid();

        var replay = await SendCreate(operation, "MILK.001", "Milk 1 L");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(productId, replayJson.RootElement.GetProperty("id").GetGuid());

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        using var list = await client.GetAsync(Path());
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("MILK.001", await list.Content.ReadAsStringAsync());

        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION"));
        await connection.OpenAsync();
        await using var audit = new NpgsqlCommand("SELECT count(*) FROM catalog.product_audit WHERE organization_id=$1 AND product_id=$2", connection);
        audit.Parameters.AddWithValue(fixture.OrganizationA);
        audit.Parameters.AddWithValue(productId);
        Assert.Equal(1L, await audit.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ReusedOperationWithDifferentPayloadConflicts()
    {
        var operation = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Created, (await SendCreate(operation, "TEA.001", "Tea")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendCreate(operation, "TEA.002", "Other Tea")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentIdempotentCreatesProduceOneProduct()
    {
        var operation = Guid.NewGuid();
        var responses = await Task.WhenAll(
            SendCreate(operation, "WATER.001", "Water"),
            SendCreate(operation, "WATER.001", "Water"));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        var ids = new List<Guid>();
        foreach (var response in responses)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ids.Add(json.RootElement.GetProperty("id").GetGuid());
        }
        Assert.Single(ids.Distinct());
    }

    [Fact]
    public async Task DuplicateSkuReturnsConflictWithoutASecondAudit()
    {
        Assert.Equal(HttpStatusCode.Created, (await SendCreate(Guid.NewGuid(), "JUICE.001", "Juice")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendCreate(Guid.NewGuid(), "JUICE.001", "Other Juice")).StatusCode);
    }

    [Fact]
    public async Task MissingPermissionAndCrossTenantRequestAreDenied()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("alice"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path())).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner", forgedClaims: true));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path(fixture.OrganizationB))).StatusCode);
    }

    [Fact]
    public async Task ProductLookupAndOptimisticUpdatePreserveHistory()
    {
        using var created = await SendCreate(Guid.NewGuid(), "BREAD.001", "Bread", "99887766");
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetGuid();
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path() + "/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path() + "/by-barcode/99887766")).StatusCode);

        var update = new { name = "Wholegrain Bread", unitCode = "EA", barcode = "99887766", isActive = false, expectedVersion = 1 };
        using var updated = await client.PutAsJsonAsync(Path() + "/" + id, update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(2, updatedJson.RootElement.GetProperty("version").GetInt64());
        Assert.False(updatedJson.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(Path() + "/" + id, update)).StatusCode);
    }

    private async Task<HttpResponseMessage> SendCreate(Guid operation, string sku, string name, string? barcode = null)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Path())
        {
            Content = JsonContent.Create(new { sku, name, unitCode = "EA", barcode })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        request.Headers.Add("Idempotency-Key", operation.ToString("D"));
        return await client.SendAsync(request);
    }
}
