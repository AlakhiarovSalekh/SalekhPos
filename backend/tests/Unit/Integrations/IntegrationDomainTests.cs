using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Domain.IntegrationConnections;
using SalekhPos.Integrations.Domain.Webhooks;

namespace SalekhPos.Tests.Integrations;

public sealed class IntegrationDomainTests
{
    [Fact]
    public void Connection_normalizes_safe_metadata_without_exposing_a_secret_value()
    {
        var connection = new IntegrationConnection(Guid.NewGuid(), Guid.NewGuid(), "  ERP  ", " Primary ERP ",
            "https://erp.example.test/hooks", "vault://tenant/erp-key", IntegrationConnectionStatus.Active);
        Assert.Equal("erp", connection.Provider); Assert.Equal("Primary ERP", connection.DisplayName);
        Assert.Equal("vault://tenant/erp-key", connection.SecretReference);
    }

    [Theory]
    [InlineData("http://example.test/hook")]
    [InlineData("https://user:password@example.test/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("not-a-uri")]
    public void Connection_rejects_unsafe_endpoint(string endpoint) =>
        Assert.Throws<ArgumentException>(() => new IntegrationConnection(Guid.NewGuid(), Guid.NewGuid(), "erp", "ERP",
            endpoint, "vault://tenant/key", IntegrationConnectionStatus.Active));

    [Theory]
    [InlineData("")]
    [InlineData("vault://tenant/key with-space")]
    [InlineData("plain-password")]
    [InlineData("https://secrets.example.test/key")]
    public void Connection_rejects_invalid_secret_reference(string reference) =>
        Assert.Throws<ArgumentException>(() => new IntegrationConnection(Guid.NewGuid(), Guid.NewGuid(), "erp", "ERP",
            "https://example.test/hook", reference, IntegrationConnectionStatus.Active));

    [Fact]
    public void Webhook_requires_exact_sha256_digest() =>
        Assert.Throws<ArgumentException>(() => new WebhookDelivery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "sale.completed", "abc", WebhookDeliveryStatus.Pending, 0));

    [Fact]
    public void Enqueue_command_validates_external_payload_reference()
    {
        var command = new EnqueueWebhookCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "sale.completed", new string('a', 64), "object://events/1");
        var delivery = command.ToModel();
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status); Assert.Equal(0, delivery.AttemptCount);
    }

    [Fact]
    public void Identity_rejects_control_characters() =>
        Assert.Throws<ArgumentException>(() => new IntegrationIdentity("issuer", "bad\nsubject").Validate());
}
