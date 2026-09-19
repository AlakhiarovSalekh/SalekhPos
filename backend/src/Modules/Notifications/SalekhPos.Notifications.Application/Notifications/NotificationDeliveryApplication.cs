using SalekhPos.Notifications.Contracts.Notifications;

namespace SalekhPos.Notifications.Application.Notifications;

public interface INotificationDeliveryStore
{
    Task<NotificationDeliveryPage> ListAsync(
        NotificationIdentity identity,
        Guid organizationId,
        int pageSize,
        Guid? after,
        string? status,
        string? channel,
        CancellationToken cancellationToken);

    Task<LeasedNotificationDeliveryResponse?> LeaseNextAsync(
        NotificationIdentity identity,
        Guid organizationId,
        int leaseSeconds,
        CancellationToken cancellationToken);

    Task<NotificationDeliveryResponse> RecordAttemptAsync(
        NotificationIdentity identity,
        Guid organizationId,
        Guid deliveryId,
        RecordNotificationDeliveryAttemptRequest request,
        CancellationToken cancellationToken);

    Task<NotificationDeliveryActivityResponse> RetryDeadLetterAsync(
        NotificationIdentity identity,
        Guid organizationId,
        Guid deliveryId,
        Guid operationId,
        string reason,
        CancellationToken cancellationToken);
}
