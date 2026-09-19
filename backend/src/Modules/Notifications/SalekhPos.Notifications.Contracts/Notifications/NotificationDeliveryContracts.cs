namespace SalekhPos.Notifications.Contracts.Notifications;

public sealed record NotificationDeliveryResponse(
    Guid Id,
    Guid NotificationId,
    string Channel,
    string RecipientSubject,
    string Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    string? LastErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LeasedNotificationDeliveryResponse(
    NotificationDeliveryResponse Delivery,
    Guid LeaseId,
    string Title,
    string Body,
    string Severity);

public sealed record RecordNotificationDeliveryAttemptRequest(
    Guid LeaseId,
    bool Succeeded,
    string? ErrorCode,
    DateTimeOffset? RetryAt);

public sealed record NotificationDeliveryActivityResponse(
    Guid Id,
    Guid NotificationId,
    string Channel,
    string RecipientSubject,
    string Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    string? LastErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Title,
    string Severity);

public sealed record NotificationDeliveryPage(
    IReadOnlyList<NotificationDeliveryActivityResponse> Items,
    Guid? NextCursor);

public sealed record RetryNotificationDeliveryRequest(string Reason);
