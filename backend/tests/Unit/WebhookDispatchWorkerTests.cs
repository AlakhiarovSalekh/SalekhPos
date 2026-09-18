using Microsoft.Extensions.Options;
using SalekhPos.Worker.Integrations;

namespace SalekhPos.Tests;

public sealed class WebhookDispatchWorkerTests
{
    [Fact]
    public void Disabled_dispatch_accepts_empty_service_identity()
    {
        var options = new WebhookDispatchOptions { Enabled = false };
        var result = new WebhookDispatchOptionsValidator().Validate(null, options);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_dispatch_requires_valid_identity()
    {
        var options = new WebhookDispatchOptions { Enabled = true, Issuer = "", Subject = "" };
        var result = new WebhookDispatchOptionsValidator().Validate(null, options);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Environment_secret_source_resolves_bounded_secret()
    {
        const string name = "SALEKHPOS_TEST_WEBHOOK_SECRET";
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, new string('x', 32));
            var source = new EnvironmentWebhookSecretSource();
            var secret = await source.ResolveAsync(new Uri("env://" + name), default);
            Assert.Equal(32, secret.Length);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }

    [Fact]
    public async Task Missing_payload_provider_is_reported_as_temporarily_unavailable()
    {
        var resolver = new CompositeWebhookPayloadResolver([]);
        await Assert.ThrowsAsync<SalekhPos.Integrations.Infrastructure.Webhooks.WebhookResolverUnavailableException>(
            async () => await resolver.ResolveAsync("object://events/1", default));
    }

    [Fact]
    public async Task Missing_environment_secret_is_reported_as_temporarily_unavailable()
    {
        const string name = "SALEKHPOS_MISSING_WEBHOOK_SECRET_FOR_TEST";
        Environment.SetEnvironmentVariable(name, null);
        var source = new EnvironmentWebhookSecretSource();
        await Assert.ThrowsAsync<SalekhPos.Integrations.Infrastructure.Webhooks.WebhookResolverUnavailableException>(
            async () => await source.ResolveAsync(new Uri("env://" + name), default));
    }

    [Fact]
    public void Dispatch_health_tracks_cycle_outcomes()
    {
        var health = new WebhookDispatchHealthState();
        var at = DateTimeOffset.Parse("2026-09-18T10:00:00Z");
        health.RecordCycle(4, 2, 1, 1, at);
        health.RecordFailure();
        var snapshot = health.Snapshot();

        Assert.Equal(1, snapshot.Cycles);
        Assert.Equal(4, snapshot.OrganizationsVisited);
        Assert.Equal(2, snapshot.Delivered);
        Assert.Equal(1, snapshot.Retried);
        Assert.Equal(1, snapshot.DeadLettered);
        Assert.Equal(1, snapshot.Failures);
        Assert.Equal(at, snapshot.LastCompletedAt);
    }
}
