using SalekhPos.Integrations.Domain.IntegrationConnections;

namespace SalekhPos.Integrations.Domain.Webhooks;

public enum WebhookDeliveryStatus { Pending, Delivering, Delivered, Failed, DeadLettered }

public sealed record WebhookDelivery
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid ConnectionId { get; }
    public Guid EventId { get; }
    public string EventType { get; }
    public string PayloadSha256 { get; }
    public WebhookDeliveryStatus Status { get; }
    public int AttemptCount { get; }

    public WebhookDelivery(Guid organizationId, Guid id, Guid connectionId, Guid eventId, string eventType,
        string payloadSha256, WebhookDeliveryStatus status, int attemptCount)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || connectionId == Guid.Empty || eventId == Guid.Empty)
            throw new ArgumentException("Webhook delivery identity is invalid.");
        EventType = IntegrationText.Required(eventType, 128, nameof(eventType), allowSpaces: false);
        PayloadSha256 = IntegrationText.Required(payloadSha256, 64, nameof(payloadSha256), allowSpaces: false).ToLowerInvariant();
        if (PayloadSha256.Length != 64 || PayloadSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Payload digest must be lowercase SHA-256 hexadecimal.", nameof(payloadSha256));
        if (attemptCount is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(attemptCount));
        OrganizationId = organizationId; Id = id; ConnectionId = connectionId; EventId = eventId;
        Status = status; AttemptCount = attemptCount;
    }
}
