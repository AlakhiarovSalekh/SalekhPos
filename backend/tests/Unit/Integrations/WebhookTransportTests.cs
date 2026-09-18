using System.Net;
using System.Security.Cryptography;
using System.Text;
using SalekhPos.Integrations.Contracts;
using SalekhPos.Integrations.Infrastructure.Webhooks;

namespace SalekhPos.Tests.Integrations;

public sealed class WebhookTransportTests
{
    [Fact]
    public async Task Dispatch_signs_verified_payload_and_accepts_success()
    {
        var payload = Encoding.UTF8.GetBytes("{\"sale\":\"ok\"}");
        var secret = Enumerable.Repeat((byte)7, 32).ToArray();
        HttpRequestMessage? captured = null;
        var transport = new WebhookTransport(new Payload(payload), new Secret(secret), new Sender(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }), new FixedTimeProvider(DateTimeOffset.Parse("2026-09-18T10:00:00Z")));

        var result = await transport.DispatchAsync(Lease(payload), default);

        Assert.True(result.Succeeded);
        Assert.Null(result.RetryAt);
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("X-SalekhPos-Signature"));
        Assert.Equal("1800266400", captured.Headers.GetValues("X-SalekhPos-Timestamp").Single());
        Assert.All(secret, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task Dispatch_rejects_payload_digest_mismatch_without_network_call()
    {
        var called = false;
        var payload = Encoding.UTF8.GetBytes("{}");
        var lease = Lease(payload) with
        {
            Delivery = Lease(payload).Delivery with { PayloadSha256 = new string('a', 64) }
        };
        var transport = new WebhookTransport(new Payload(payload), new Secret(Enumerable.Repeat((byte)1, 32).ToArray()),
            new Sender(_ => { called = true; return new HttpResponseMessage(HttpStatusCode.OK); }),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await transport.DispatchAsync(lease, default);

        Assert.False(called);
        Assert.False(result.Succeeded);
        Assert.Equal("payload_digest_mismatch", result.ErrorCode);
        Assert.Null(result.RetryAt);
    }

    [Fact]
    public async Task Dispatch_classifies_retryable_http_status()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var transport = new WebhookTransport(new Payload(payload), new Secret(Enumerable.Repeat((byte)2, 32).ToArray()),
            new Sender(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-09-18T10:00:00Z")));

        var result = await transport.DispatchAsync(Lease(payload), default);

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("http_503", result.ErrorCode);
        Assert.Equal(DateTimeOffset.Parse("2026-09-18T10:00:05Z"), result.RetryAt);
    }

    private static LeasedWebhookResponse Lease(byte[] payload)
    {
        var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var delivery = new WebhookDeliveryResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sale.completed",
            digest, "delivering", 0, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return new(delivery, Guid.NewGuid(), "object://events/1", "https://webhook.example.test/events",
            "vault://tenant/webhook");
    }

    private sealed class Payload(byte[] value) : IWebhookPayloadResolver
    {
        public ValueTask<ReadOnlyMemory<byte>> ResolveAsync(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>>(value);
    }

    private sealed class Secret(byte[] value) : IWebhookSecretResolver
    {
        public ValueTask<byte[]> ResolveAsync(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult(value);
    }

    private sealed class Sender(Func<HttpRequestMessage, HttpResponseMessage> send) : IWebhookHttpSender
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
