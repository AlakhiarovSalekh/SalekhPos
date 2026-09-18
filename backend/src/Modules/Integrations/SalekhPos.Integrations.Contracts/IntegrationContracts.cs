namespace SalekhPos.Integrations.Contracts;

public sealed record CreateIntegrationConnectionRequest(string Provider, string DisplayName, string Endpoint, string SecretReference);
public sealed record IntegrationConnectionResponse(Guid Id, string Provider, string DisplayName, string Endpoint,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record IntegrationConnectionPage(IReadOnlyList<IntegrationConnectionResponse> Items, Guid? NextCursor);
public sealed record DisableIntegrationConnectionRequest(string Reason);

public sealed record EnqueueWebhookRequest(Guid ConnectionId, Guid EventId, string EventType,
    string PayloadSha256, string PayloadReference);
public sealed record EnqueueStoredWebhookRequest(Guid ConnectionId, Guid EventId, string EventType,
    System.Text.Json.JsonElement Payload);
public sealed record WebhookDeliveryResponse(Guid Id, Guid ConnectionId, Guid EventId, string EventType,
    string PayloadSha256, string Status, int AttemptCount, DateTimeOffset? NextAttemptAt,
    int? LastStatusCode, string? LastErrorCode, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record WebhookDeliveryPage(IReadOnlyList<WebhookDeliveryResponse> Items, Guid? NextCursor);
public sealed record LeaseWebhookRequest(int LeaseSeconds);
public sealed record LeasedWebhookResponse(WebhookDeliveryResponse Delivery, Guid LeaseId, string PayloadReference,
    string Endpoint, string SecretReference);
public sealed record RecordWebhookAttemptRequest(Guid LeaseId, bool Succeeded, int? StatusCode,
    string? ErrorCode, DateTimeOffset? RetryAt);
