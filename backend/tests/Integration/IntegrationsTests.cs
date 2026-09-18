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
        var (organizationId, subject) = await CreateWebhookTenantAsync(
            "integrations.manage", "integrations.view", "integrations.dispatch");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        var root = $"/api/v1/organizations/{organizationId:D}/integrations";

        using var connectionResponse = await Post(client, root + "/connections",
            new
            {
                Provider = "generic.http",
                DisplayName = "Test webhook",
                Endpoint = "https://example.test/webhook",
                SecretReference = "vault://tenant/test-webhook"
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, connectionResponse.StatusCode);
        using var connectionJson = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
        var connectionId = connectionJson.RootElement.GetProperty("id").GetGuid();

        using var enqueue = await Post(client, root + "/webhooks",
            new
            {
                ConnectionId = connectionId,
                EventId = Guid.NewGuid(),
                EventType = "sale.completed",
                PayloadSha256 = new string('a', 64),
                PayloadReference = "object://events/test"
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, enqueue.StatusCode);

        using var lease = await client.PostAsJsonAsync(root + "/webhooks/lease", new { LeaseSeconds = 30 });
        Assert.Equal(HttpStatusCode.OK, lease.StatusCode);
        using var leaseJson = JsonDocument.Parse(await lease.Content.ReadAsStringAsync());
        var deliveryId = leaseJson.RootElement.GetProperty("delivery").GetProperty("id").GetGuid();
        var leaseId = leaseJson.RootElement.GetProperty("leaseId").GetGuid();

        using var attempt = await client.PostAsJsonAsync(root + $"/webhooks/{deliveryId:D}/attempts",
            new
            {
                LeaseId = leaseId,
                Succeeded = false,
                StatusCode = 422,
                ErrorCode = "http_422",
                RetryAt = (DateTimeOffset?)null
            });
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        using var attemptJson = JsonDocument.Parse(await attempt.Content.ReadAsStringAsync());
        Assert.Equal("dead_lettered", attemptJson.RootElement.GetProperty("status").GetString());

        using var secondLease = await client.PostAsJsonAsync(root + "/webhooks/lease", new { LeaseSeconds = 30 });
        Assert.Equal(HttpStatusCode.NoContent, secondLease.StatusCode);
    }

    [Fact]
    public async Task Dead_lettered_webhook_can_be_manually_requeued_with_audit()
    {
        var (organizationId, subject) = await CreateWebhookTenantAsync("integrations.manage", "integrations.dispatch");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        var root = $"/api/v1/organizations/{organizationId:D}/integrations";

        using var connectionResponse = await Post(client, root + "/connections",
            new
            {
                Provider = "generic.http",
                DisplayName = "Manual retry webhook",
                Endpoint = "https://example.test/webhook",
                SecretReference = "env://SALEKHPOS_TEST_WEBHOOK_SECRET"
            },
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, connectionResponse.StatusCode);
        using var connectionJson = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
        var connectionId = connectionJson.RootElement.GetProperty("id").GetGuid();

        using var enqueue = await Post(client, root + "/webhooks",
            new
            {
                ConnectionId = connectionId,
                EventId = Guid.NewGuid(),
                EventType = "sale.completed",
                PayloadSha256 = new string('b', 64),
                PayloadReference = "object://events/manual-retry"
            },
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, enqueue.StatusCode);

        using var lease = await client.PostAsJsonAsync(root + "/webhooks/lease", new { LeaseSeconds = 30 });
        Assert.Equal(HttpStatusCode.OK, lease.StatusCode);
        using var leaseJson = JsonDocument.Parse(await lease.Content.ReadAsStringAsync());
        var deliveryId = leaseJson.RootElement.GetProperty("delivery").GetProperty("id").GetGuid();
        var leaseId = leaseJson.RootElement.GetProperty("leaseId").GetGuid();

        using var terminal = await client.PostAsJsonAsync(root + $"/webhooks/{deliveryId:D}/attempts",
            new
            {
                LeaseId = leaseId,
                Succeeded = false,
                StatusCode = 422,
                ErrorCode = "http_422",
                RetryAt = (DateTimeOffset?)null
            });
        Assert.Equal(HttpStatusCode.OK, terminal.StatusCode);

        var operation = Guid.NewGuid();
        const string reason = "Provider endpoint corrected by operator";
        using var retried = await Post(client, root + $"/webhooks/{deliveryId:D}/retry",
            new { Reason = reason }, operation);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        using var retriedJson = JsonDocument.Parse(await retried.Content.ReadAsStringAsync());
        Assert.Equal("pending", retriedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, retriedJson.RootElement.GetProperty("attemptCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, retriedJson.RootElement.GetProperty("lastErrorCode").ValueKind);

        using var replay = await Post(client, root + $"/webhooks/{deliveryId:D}/retry",
            new { Reason = reason }, operation);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        await using var source = Npgsql.NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION")!);
        await using var audit = source.CreateCommand("""
            SELECT previous_attempt_count,reason,count(*) OVER()
            FROM integrations.webhook_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """);
        audit.Parameters.AddWithValue(organizationId);
        audit.Parameters.AddWithValue(operation);
        await using var auditReader = await audit.ExecuteReaderAsync();
        Assert.True(await auditReader.ReadAsync());
        Assert.Equal(1, auditReader.GetInt32(0));
        Assert.Equal(reason, auditReader.GetString(1));
        Assert.Equal(1L, auditReader.GetInt64(2));
    }

    [Fact]
    public async Task Stored_webhook_persists_immutable_tenant_payload_and_delivery_digest()
    {
        var (organizationId, subject) = await CreateWebhookTenantAsync("integrations.manage", "integrations.dispatch");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        var root = $"/api/v1/organizations/{organizationId:D}/integrations";

        using var connectionResponse = await Post(client, root + "/connections",
            new
            {
                Provider = "generic.http",
                DisplayName = "Stored payload webhook",
                Endpoint = "https://example.test/webhook",
                SecretReference = "env://SALEKHPOS_TEST_WEBHOOK_SECRET"
            },
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, connectionResponse.StatusCode);
        using var connectionJson = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
        var connectionId = connectionJson.RootElement.GetProperty("id").GetGuid();

        var eventId = Guid.NewGuid();
        using var response = await Post(client, root + "/webhooks/stored",
            new
            {
                ConnectionId = connectionId,
                EventId = eventId,
                EventType = "sale.completed",
                Payload = new { saleId = Guid.NewGuid(), total = 42.50m, currency = "GEL" }
            }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var deliveryId = responseJson.RootElement.GetProperty("id").GetGuid();
        var digest = responseJson.RootElement.GetProperty("payloadSha256").GetString();
        Assert.Matches("^[0-9a-f]{64}$", digest!);

        await using var source = Npgsql.NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION")!);
        await using var command = source.CreateCommand("""
            SELECT d.payload_reference,p.payload_sha256,octet_length(p.payload)
            FROM integrations.webhook_deliveries d
            JOIN integrations.webhook_payloads p
              ON p.organization_id=d.organization_id AND p.delivery_id=d.delivery_id
            WHERE d.organization_id=$1 AND d.delivery_id=$2
            """);
        command.Parameters.AddWithValue(organizationId);
        command.Parameters.AddWithValue(deliveryId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal($"pgpayload://{organizationId:D}/{deliveryId:D}", reader.GetString(0));
        Assert.Equal(digest, reader.GetString(1));
        Assert.InRange(reader.GetInt32(2), 1, 262_144);
    }

    [Fact]
    public async Task Duplicate_event_with_new_operation_is_a_conflict()
    {
        var (organizationId, subject) = await CreateWebhookTenantAsync("integrations.manage", "integrations.dispatch");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        var root = $"/api/v1/organizations/{organizationId:D}/integrations";

        using var connectionResponse = await Post(client, root + "/connections",
            new
            {
                Provider = "generic.http",
                DisplayName = "Duplicate event webhook",
                Endpoint = "https://example.test/webhook",
                SecretReference = "env://SALEKHPOS_TEST_WEBHOOK_SECRET"
            },
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, connectionResponse.StatusCode);
        using var connectionJson = JsonDocument.Parse(await connectionResponse.Content.ReadAsStringAsync());
        var connectionId = connectionJson.RootElement.GetProperty("id").GetGuid();
        var eventId = Guid.NewGuid();
        var payload = new
        {
            ConnectionId = connectionId,
            EventId = eventId,
            EventType = "sale.completed",
            Payload = new { saleId = Guid.NewGuid() }
        };

        using var first = await Post(client, root + "/webhooks/stored", payload, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await Post(client, root + "/webhooks/stored", payload, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    private async Task<(Guid OrganizationId, string Subject)> CreateWebhookTenantAsync(params string[] permissions)
    {
        var organizationId = Guid.NewGuid();
        var subject = "webhook-" + Guid.NewGuid().ToString("N");
        await fixture.ExecuteAsync(
            "INSERT INTO organization.organizations(organization_id,name) VALUES($1,$2)",
            organizationId,
            "Webhook integration test " + organizationId.ToString("N"));
        await fixture.AddMembershipAsync(subject, organizationId, null);
        foreach (var permission in permissions)
        {
            await fixture.GrantAsync(subject, organizationId, permission);
        }
        return (organizationId, subject);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, Guid operationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request);
    }
}
