using SalekhPos.Notifications.Contracts.Notifications;

namespace SalekhPos.Notifications.Application.Notifications;

public interface INotificationDeliveryStore
{
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
}
