using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalekhPos.Authorization.Application;
using SalekhPos.Notifications.Application.Notifications;
using SalekhPos.Worker.Schedulers;

namespace SalekhPos.Worker.Notifications;

public sealed class NotificationDeliveryHostedService(
    IOptions<NotificationDispatchOptions> options,
    IAccessibleOrganizationReader organizations,
    NotificationTenantDispatcher dispatcher,
    NotificationProviderTransport transport,
    NotificationDispatchHealthState health,
    IWorkerDelay delay,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryHostedService> logger) : BackgroundService
{
    private readonly NotificationDispatchOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("External notification dispatch polling is disabled");
            return;
        }

        var accessIdentity = new AccessIdentity(settings.Issuer, settings.Subject);
        var notificationIdentity = new NotificationIdentity(settings.Issuer, settings.Subject);
        notificationIdentity.Validate();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!transport.IsReady)
                {
                    health.RecordConfigurationBlock();
                    logger.LogWarning("Notification dispatch is enabled but provider credentials are unavailable");
                }
                else
                {
                    await RunCycle(accessIdentity, notificationIdentity, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error) when (error is AccessUnavailableException or NotificationUnavailableException)
            {
                health.RecordFailure();
                logger.LogError("Notification dispatch cycle failed because a required persistence dependency is unavailable");
            }

            try
            {
                await delay.DelayAsync(settings.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunCycle(
        AccessIdentity accessIdentity,
        NotificationIdentity notificationIdentity,
        CancellationToken cancellationToken)
    {
        Guid? after = null;
        var organizationsVisited = 0;
        var leased = 0;
        var delivered = 0;
        var retried = 0;
        var deadLettered = 0;

        do
        {
            var page = await organizations.ReadAsync(
                accessIdentity,
                settings.OrganizationPageSize,
                after,
                cancellationToken);

            foreach (var organization in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                organizationsVisited++;
                try
                {
                    var result = await dispatcher.DispatchDueAsync(
                        notificationIdentity,
                        organization.Id,
                        settings.MaximumDeliveriesPerOrganization,
                        settings.LeaseSeconds,
                        cancellationToken);
                    leased += result.Leased;
                    delivered += result.Delivered;
                    retried += result.Retried;
                    deadLettered += result.DeadLettered;
                }
                catch (NotificationDeniedException)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Notification dispatcher has no dispatch grant for organization {OrganizationId}",
                            organization.Id);
                    }
                }
                catch (NotificationConflictException)
                {
                    health.RecordFailure();
                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(
                            "Notification delivery state changed concurrently for organization {OrganizationId}",
                            organization.Id);
                    }
                }
            }

            after = page.NextCursor;
        }
        while (after.HasValue);

        health.RecordCycle(
            organizationsVisited,
            leased,
            delivered,
            retried,
            deadLettered,
            timeProvider.GetUtcNow());

        if (leased > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Notification dispatch cycle processed {Organizations} organizations with {Leased} leased, {Delivered} delivered, {Retried} retryable and {DeadLettered} dead-lettered deliveries",
                organizationsVisited,
                leased,
                delivered,
                retried,
                deadLettered);
        }
    }
}
