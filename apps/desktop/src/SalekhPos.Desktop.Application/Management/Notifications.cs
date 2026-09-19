namespace SalekhPos.Desktop.Application.Management;

public sealed record DesktopNotificationDelivery(
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
    string Severity)
{
    public override string ToString() =>
        $"{Title} · {Channel} · {Status} · attempts {AttemptCount}";
}

public sealed record DesktopNotificationDeliveryPage(
    IReadOnlyList<DesktopNotificationDelivery> Items,
    Guid? NextCursor);

public interface INotificationDeliveryManager
{
    Task<DesktopNotificationDeliveryPage> DeliveriesAsync(
        Guid organizationId,
        string? status,
        string? channel,
        CancellationToken cancellationToken);

    Task<DesktopNotificationDelivery> RetryAsync(
        Guid organizationId,
        Guid deliveryId,
        string reason,
        Guid operationId,
        CancellationToken cancellationToken);
}
