using SalekhPos.Notifications.Application.Notifications;

namespace SalekhPos.Worker.Notifications;

public sealed record NotificationDispatchBatchResult(
    int Leased,
    int Delivered,
    int Retried,
    int DeadLettered);

public sealed class NotificationTenantDispatcher(
    INotificationDeliveryStore deliveries,
    NotificationProviderTransport transport)
{
    public async Task<NotificationDispatchBatchResult> DispatchDueAsync(
        NotificationIdentity identity,
        Guid organizationId,
        int maximumDeliveries,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (maximumDeliveries is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));
        if (leaseSeconds is < 10 or > 300) throw new ArgumentOutOfRangeException(nameof(leaseSeconds));

        var leased = 0;
        var delivered = 0;
        var retried = 0;
        var deadLettered = 0;

        while (leased < maximumDeliveries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await deliveries.LeaseNextAsync(
                identity,
                organizationId,
                leaseSeconds,
                cancellationToken);
            if (item is null) break;

            leased++;
            NotificationTransportResult result;
            try
            {
                result = await transport.DispatchAsync(item, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                result = new(false, null, "dispatch_validation_failed", null);
            }

            var recorded = await deliveries.RecordAttemptAsync(
                identity,
                organizationId,
                item.Delivery.Id,
                result.ToAttempt(item.LeaseId),
                cancellationToken);

            switch (recorded.Status)
            {
                case "delivered":
                    delivered++;
                    break;
                case "failed":
                    retried++;
                    break;
                case "dead_lettered":
                    deadLettered++;
                    break;
                default:
                    throw new NotificationUnavailableException();
            }
        }

        return new(leased, delivered, retried, deadLettered);
    }
}
