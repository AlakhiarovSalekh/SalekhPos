using System.Net;
using Microsoft.Extensions.Options;
using SalekhPos.Integrations.Infrastructure.Webhooks;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Notifications.Contracts.Notifications;
using SalekhPos.Worker.Notifications;

namespace SalekhPos.Tests;

public sealed class NotificationDispatchWorkerTests
{
    [Fact]
    public void Disabled_dispatch_accepts_empty_provider_configuration()
    {
        var result = new NotificationDispatchOptionsValidator().Validate(
            null,
            new NotificationDispatchOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_dispatch_requires_safe_endpoints_identity_and_environment_names()
    {
        var options = ValidOptions();
        var validator = new NotificationDispatchOptionsValidator();

        Assert.True(validator.Validate(null, options).Succeeded);

        options.EmailEndpoint = "http://127.0.0.1/send";
        options.EmailCredentialEnvironmentVariable = "invalid-name-with-dash";
        var invalid = validator.Validate(null, options);

        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Failures!, failure => failure.Contains(nameof(options.EmailEndpoint), StringComparison.Ordinal));
        Assert.Contains(invalid.Failures!, failure => failure.Contains(nameof(options.EmailCredentialEnvironmentVariable), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Transport_sends_subject_reference_without_exposing_provider_credential_in_payload()
    {
        var options = ValidOptions();
        var credential = new string('a', 32);
        var originalEmail = Environment.GetEnvironmentVariable(options.EmailCredentialEnvironmentVariable);
        var originalPush = Environment.GetEnvironmentVariable(options.PushCredentialEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, credential);
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, new string('b', 32));
            var sender = new RecordingSender(HttpStatusCode.Accepted);
            var transport = new NotificationProviderTransport(
                Options.Create(options),
                sender,
                new FixedTimeProvider(DateTimeOffset.Parse("2026-09-18T20:00:00Z")));

            Assert.True(transport.IsReady);
            var result = await transport.DispatchAsync(Lease("email", attemptCount: 0), default);

            Assert.True(result.Succeeded);
            Assert.Equal(202, result.StatusCode);
            Assert.Equal("Bearer " + credential, sender.Authorization);
            Assert.Contains("\"recipientSubject\":\"recipient-subject\"", sender.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(credential, sender.Body, StringComparison.Ordinal);
            Assert.Equal("application/json", sender.ContentType);
        }
        finally
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, originalEmail);
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, originalPush);
        }
    }

    [Fact]
    public async Task Transport_retries_transient_provider_failure_and_dead_letters_permanent_failure()
    {
        var options = ValidOptions();
        var originalEmail = Environment.GetEnvironmentVariable(options.EmailCredentialEnvironmentVariable);
        var originalPush = Environment.GetEnvironmentVariable(options.PushCredentialEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, new string('c', 32));
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, new string('d', 32));
            var now = DateTimeOffset.Parse("2026-09-18T20:00:00Z");

            var transient = new NotificationProviderTransport(
                Options.Create(options),
                new RecordingSender(HttpStatusCode.ServiceUnavailable),
                new FixedTimeProvider(now));
            var transientResult = await transient.DispatchAsync(Lease("push", attemptCount: 0), default);
            Assert.False(transientResult.Succeeded);
            Assert.Equal("http_503", transientResult.ErrorCode);
            Assert.Equal(now.AddSeconds(5), transientResult.RetryAt);

            var permanent = new NotificationProviderTransport(
                Options.Create(options),
                new RecordingSender(HttpStatusCode.BadRequest),
                new FixedTimeProvider(now));
            var permanentResult = await permanent.DispatchAsync(Lease("email", attemptCount: 0), default);
            Assert.False(permanentResult.Succeeded);
            Assert.Equal("http_400", permanentResult.ErrorCode);
            Assert.Null(permanentResult.RetryAt);
        }
        finally
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, originalEmail);
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, originalPush);
        }
    }

    [Fact]
    public async Task Tenant_dispatcher_records_delivered_retryable_and_dead_lettered_results()
    {
        var options = ValidOptions();
        var originalEmail = Environment.GetEnvironmentVariable(options.EmailCredentialEnvironmentVariable);
        var originalPush = Environment.GetEnvironmentVariable(options.PushCredentialEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, new string('e', 32));
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, new string('f', 32));
            var leases = new[]
            {
                Lease("email", 0),
                Lease("push", 0),
                Lease("email", 0),
            };
            var store = new FakeDeliveryStore(leases);
            var sender = new SequenceSender(
                HttpStatusCode.Accepted,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.BadRequest);
            var transport = new NotificationProviderTransport(
                Options.Create(options),
                sender,
                new FixedTimeProvider(DateTimeOffset.UtcNow));
            var dispatcher = new NotificationTenantDispatcher(store, transport);
            var identity = new NotificationIdentity("https://worker.example", "notification-worker");

            var result = await dispatcher.DispatchDueAsync(
                identity,
                Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                10,
                60,
                default);

            Assert.Equal(3, result.Leased);
            Assert.Equal(1, result.Delivered);
            Assert.Equal(1, result.Retried);
            Assert.Equal(1, result.DeadLettered);
            Assert.Equal(3, store.Attempts.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable(options.EmailCredentialEnvironmentVariable, originalEmail);
            Environment.SetEnvironmentVariable(options.PushCredentialEnvironmentVariable, originalPush);
        }
    }

    [Fact]
    public void Dispatch_health_accumulates_cycle_and_failure_signals()
    {
        var health = new NotificationDispatchHealthState();
        var completedAt = DateTimeOffset.Parse("2026-09-18T20:00:00Z");

        health.RecordConfigurationBlock();
        health.RecordFailure();
        health.RecordCycle(4, 6, 3, 2, 1, completedAt);
        var snapshot = health.Snapshot();

        Assert.Equal(1, snapshot.Cycles);
        Assert.Equal(4, snapshot.OrganizationsVisited);
        Assert.Equal(6, snapshot.Leased);
        Assert.Equal(3, snapshot.Delivered);
        Assert.Equal(2, snapshot.Retried);
        Assert.Equal(1, snapshot.DeadLettered);
        Assert.Equal(1, snapshot.ConfigurationBlocks);
        Assert.Equal(1, snapshot.Failures);
        Assert.Equal(completedAt, snapshot.LastCompletedAt);
    }

    private static NotificationDispatchOptions ValidOptions()
    {
        var suffix = Guid.NewGuid().ToString("N").ToUpperInvariant();
        return new NotificationDispatchOptions
        {
            Enabled = true,
            Issuer = "https://worker.example",
            Subject = "notification-worker",
            PollInterval = TimeSpan.FromSeconds(5),
            OrganizationPageSize = 50,
            MaximumDeliveriesPerOrganization = 20,
            LeaseSeconds = 60,
            EmailEndpoint = "https://email-provider.example.test/send",
            PushEndpoint = "https://push-provider.example.test/send",
            EmailCredentialEnvironmentVariable = "SALEKHPOS_EMAIL_TEST_" + suffix,
            PushCredentialEnvironmentVariable = "SALEKHPOS_PUSH_TEST_" + suffix,
        };
    }

    private static LeasedNotificationDeliveryResponse Lease(string channel, int attemptCount)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = new NotificationDeliveryResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            channel,
            "recipient-subject",
            "delivering",
            attemptCount,
            null,
            null,
            now,
            now);
        return new(
            delivery,
            Guid.NewGuid(),
            "Security alert",
            "A security-relevant event requires attention.",
            "warning");
    }

    private sealed class RecordingSender(HttpStatusCode status) : IWebhookHttpSender
    {
        public string Authorization { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;

        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.TryGetValues("Authorization", out var values)
                ? values.Single()
                : string.Empty;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content.Headers.ContentType?.MediaType ?? string.Empty;
            return new HttpResponseMessage(status);
        }
    }

    private sealed class SequenceSender(params HttpStatusCode[] statuses) : IWebhookHttpSender
    {
        private readonly Queue<HttpStatusCode> pending = new(statuses);

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(pending.Dequeue()));
        }
    }

    private sealed class FakeDeliveryStore(IEnumerable<LeasedNotificationDeliveryResponse> leases)
        : INotificationDeliveryStore
    {
        private readonly Queue<LeasedNotificationDeliveryResponse> pending = new(leases);
        private readonly Dictionary<Guid, LeasedNotificationDeliveryResponse> byId =
            leases.ToDictionary(item => item.Delivery.Id);

        public List<RecordNotificationDeliveryAttemptRequest> Attempts { get; } = [];

        public Task<LeasedNotificationDeliveryResponse?> LeaseNextAsync(
            NotificationIdentity identity,
            Guid organizationId,
            int leaseSeconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<LeasedNotificationDeliveryResponse?>(
                pending.Count == 0 ? null : pending.Dequeue());
        }

        public Task<NotificationDeliveryResponse> RecordAttemptAsync(
            NotificationIdentity identity,
            Guid organizationId,
            Guid deliveryId,
            RecordNotificationDeliveryAttemptRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts.Add(request);
            var item = byId[deliveryId].Delivery;
            var status = request.Succeeded
                ? "delivered"
                : request.RetryAt.HasValue ? "failed" : "dead_lettered";
            return Task.FromResult(item with
            {
                Status = status,
                AttemptCount = item.AttemptCount + 1,
                NextAttemptAt = request.RetryAt,
                LastErrorCode = request.ErrorCode,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
