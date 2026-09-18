using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Contracts;

namespace SalekhPos.Integrations.Infrastructure.Webhooks;

public sealed record WebhookDispatchBatchResult(int Leased, int Delivered, int Retried, int DeadLettered, int Deferred);

public sealed class WebhookTenantDispatcher(
    IIntegrationService integrations,
    WebhookTransport transport,
    TimeProvider timeProvider)
{
    public async Task<WebhookDispatchBatchResult> DispatchDueAsync(
        IntegrationIdentity identity,
        Guid organizationId,
        int maximumDeliveries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (maximumDeliveries is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumDeliveries));

        var leased = 0;
        var delivered = 0;
        var retried = 0;
        var deadLettered = 0;
        var deferred = 0;

        while (leased < maximumDeliveries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await integrations.LeaseNextWebhookAsync(
                identity,
                organizationId,
                new LeaseWebhookRequest(30),
                cancellationToken);
            if (item is null) break;

            leased++;
            WebhookTransportResult result;
            try
            {
                result = await transport.DispatchAsync(item, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (WebhookResolverUnavailableException)
            {
                var deferredDelivery = await integrations.DeferLeaseAsync(
                    identity,
                    organizationId,
                    item.Delivery.Id,
                    new(item.LeaseId, "resolver_unavailable", timeProvider.GetUtcNow().AddMinutes(5)),
                    cancellationToken);
                if (deferredDelivery.Status != "failed"
                    || deferredDelivery.AttemptCount != item.Delivery.AttemptCount)
                {
                    throw new IntegrationUnavailableException();
                }
                deferred++;
                continue;
            }
            catch (IntegrationNotFoundException)
            {
                result = new(false, null, "payload_not_found", null);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                result = new(false, null, "dispatch_validation_failed", null);
            }

            var recorded = await integrations.RecordAttemptAsync(
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
                    throw new IntegrationUnavailableException();
            }
        }

        return new(leased, delivered, retried, deadLettered, deferred);
    }
}
