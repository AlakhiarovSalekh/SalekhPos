using System.Net;
using System.Security.Cryptography;
using System.Text;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Contracts;
using SalekhPos.Integrations.Infrastructure.Webhooks;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class WebhookTenantDispatcherTests
{
    [Fact]
    public async Task Dispatcher_leases_dispatches_and_records_success()
    {
        var organizationId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{\"ok\":true}");
        var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var lease = new LeasedWebhookResponse(
            new WebhookDeliveryResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sale.completed",
                digest, "delivering", 0, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            Guid.NewGuid(), "object://events/1", "https://webhook.example.test/events", "vault://tenant/webhook");

        var service = new FakeIntegrationService(lease);
        var transport = new WebhookTransport(
            new Payload(payload),
            new Secret([.. Enumerable.Repeat((byte)5, 32)]),
            new Sender(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            TimeProvider.System);
        var dispatcher = new WebhookTenantDispatcher(service, transport, TimeProvider.System);

        var result = await dispatcher.DispatchDueAsync(
            new IntegrationIdentity("https://worker.example.test", "integration-worker"),
            organizationId, 10, default);

        Assert.Equal(1, result.Leased);
        Assert.Equal(1, result.Delivered);
        Assert.Equal(0, result.Retried);
        Assert.Equal(0, result.DeadLettered);
        Assert.Equal(0, result.Deferred);
        Assert.Equal(1, service.RecordedAttempts);
        Assert.Equal(0, service.DeferredLeases);
    }

    [Fact]
    public async Task Dispatcher_defers_resolver_outage_without_consuming_attempt()
    {
        var organizationId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("{}");
        var lease = new LeasedWebhookResponse(
            new WebhookDeliveryResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sale.completed",
                Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), "delivering", 4,
                null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            Guid.NewGuid(), "object://missing/1", "https://webhook.example.test/events", "vault://missing/key");

        var service = new FakeIntegrationService(lease);
        var transport = new WebhookTransport(
            new UnavailablePayload(),
            new Secret([.. Enumerable.Repeat((byte)5, 32)]),
            new Sender(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            TimeProvider.System);
        var dispatcher = new WebhookTenantDispatcher(service, transport,
            new FixedTimeProvider(DateTimeOffset.Parse("2026-09-18T11:00:00Z")));

        var result = await dispatcher.DispatchDueAsync(
            new IntegrationIdentity("https://worker.example.test", "integration-worker"),
            organizationId, 10, default);

        Assert.Equal(1, result.Leased);
        Assert.Equal(1, result.Deferred);
        Assert.Equal(0, result.Retried);
        Assert.Equal(0, service.RecordedAttempts);
        Assert.Equal(1, service.DeferredLeases);
        Assert.Equal(4, service.LastDeferredAttemptCount);
    }

    private sealed class FakeIntegrationService(LeasedWebhookResponse lease) : IIntegrationService
    {
        private bool leased;
        public int RecordedAttempts { get; private set; }
        public int DeferredLeases { get; private set; }
        public int LastDeferredAttemptCount { get; private set; } = -1;

        public Task<LeasedWebhookResponse?> LeaseNextWebhookAsync(IntegrationIdentity identity, Guid organizationId,
            LeaseWebhookRequest request, CancellationToken cancellationToken)
        {
            if (leased) return Task.FromResult<LeasedWebhookResponse?>(null);
            leased = true;
            return Task.FromResult<LeasedWebhookResponse?>(lease);
        }

        public Task<WebhookDeliveryResponse> RecordAttemptAsync(IntegrationIdentity identity, Guid organizationId,
            Guid deliveryId, RecordWebhookAttemptRequest request, CancellationToken cancellationToken)
        {
            RecordedAttempts++;
            return Task.FromResult(lease.Delivery with
            {
                Status = request.Succeeded ? "delivered" : request.RetryAt.HasValue ? "failed" : "dead_lettered",
                AttemptCount = lease.Delivery.AttemptCount + 1,
                LastStatusCode = request.StatusCode,
                LastErrorCode = request.ErrorCode,
                NextAttemptAt = request.RetryAt
            });
        }

        public Task<WebhookDeliveryResponse> DeferLeaseAsync(IntegrationIdentity identity, Guid organizationId,
            Guid deliveryId, DeferWebhookLeaseCommand command, CancellationToken cancellationToken)
        {
            DeferredLeases++;
            LastDeferredAttemptCount = lease.Delivery.AttemptCount;
            return Task.FromResult(lease.Delivery with
            {
                Status = "failed",
                NextAttemptAt = command.RetryAt,
                LastErrorCode = command.ErrorCode
            });
        }

        public Task<WebhookDeliveryResponse> RetryDeadLetterAsync(IntegrationIdentity identity, Guid organizationId,
            Guid deliveryId, Guid operationId, string reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntegrationWriteResult<IntegrationConnectionResponse>> CreateConnectionAsync(IntegrationIdentity identity,
            CreateIntegrationConnectionCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntegrationConnectionPage> ListConnectionsAsync(IntegrationIdentity identity, Guid organizationId,
            int pageSize, Guid? after, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntegrationConnectionResponse> DisableConnectionAsync(IntegrationIdentity identity, Guid organizationId,
            Guid connectionId, Guid operationId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntegrationWriteResult<WebhookDeliveryResponse>> EnqueueWebhookAsync(IntegrationIdentity identity,
            EnqueueWebhookCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WebhookDeliveryPage> ListDeliveriesAsync(IntegrationIdentity identity, Guid organizationId,
            int pageSize, Guid? after, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnavailablePayload : IWebhookPayloadResolver
    {
        public ValueTask<ReadOnlyMemory<byte>> ResolveAsync(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromException<ReadOnlyMemory<byte>>(
                new WebhookResolverUnavailableException("Payload store is temporarily unavailable."));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
}
