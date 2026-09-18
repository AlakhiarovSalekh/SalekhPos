using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class IntegrationsTests(AccessFixture fixture)
{
    [Fact]
    public async Task Terminal_webhook_attempt_is_dead_lettered_and_not_released_again()
    {
        await fixture.GrantAsync("owner", fixture.OrganizationA, "integrations.manage");
        await fixture.GrantAsync("owner", fixture.OrganizationA, "integrations.view");
        await fixture.GrantAsync("owner", fixture.OrganizationA, "integrations.dispatch");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        var root = $"/api/v1/organizations/{fixture.OrganizationA:D}/integrations";

        using var connectionResponse = await Post(client, root + "/connections",
            new { Provider = "generic.http", DisplayName = "Test webhook", Endpoint = "https://example.test/webhook",
                SecretReference = "vault://tenant/test-webhook" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, connectionResponse.StatusCode);
        using var connectionJson = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
        var connectionId = connectionJson.RootElement.GetProperty("id").GetGuid();

        using var enqueue = await Post(client, root + "/webhooks",
            new { ConnectionId = connectionId, EventId = Guid.NewGuid(), EventType = "sale.completed",
                PayloadSha256 = new string('a', 64), PayloadReference = "object://events/test" }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, enqueue.StatusCode);

        using var lease = await client.PostAsJsonAsync(root + "/webhooks/lease", new { LeaseSeconds = 30 });
        Assert.Equal(HttpStatusCode.OK, lease.StatusCode);
        using var leaseJson = JsonDocument.Parse(await lease.Content.ReadAsStringAsync());
        var deliveryId = leaseJson.RootElement.GetProperty("delivery").GetProperty("id").GetGuid();
        var leaseId = leaseJson.RootElement.GetProperty("leaseId").GetGuid();

        using var attempt = await client.PostAsJsonAsync(root + $"/webhooks/{deliveryId:D}/attempts",
            new { LeaseId = leaseId, Succeeded = false, StatusCode = 422, ErrorCode = "http_422",
                RetryAt = (DateTimeOffset?)null });
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        using var attemptJson = JsonDocument.Parse(await attempt.Content.ReadAsStringAsync());
        Assert.Equal("dead_lettered", attemptJson.RootElement.GetProperty("status").GetString());

        using var secondLease = await client.PostAsJsonAsync(root + "/webhooks/lease", new { LeaseSeconds = 30 });
        Assert.Equal(HttpStatusCode.NoContent, secondLease.StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, Guid operationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request);
    }
}
