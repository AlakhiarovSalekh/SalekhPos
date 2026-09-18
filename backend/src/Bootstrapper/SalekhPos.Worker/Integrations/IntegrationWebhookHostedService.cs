using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalekhPos.Authorization.Application;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Infrastructure.Webhooks;
using SalekhPos.Worker.Schedulers;

namespace SalekhPos.Worker.Integrations;

public sealed class IntegrationWebhookHostedService(
    IOptions<WebhookDispatchOptions> options,
    IAccessibleOrganizationReader organizations,
    WebhookTenantDispatcher dispatcher,
    CompositeWebhookPayloadResolver payloads,
    CompositeWebhookSecretResolver secrets,
    WebhookDispatchHealthState health,
    IWorkerDelay delay,
    TimeProvider timeProvider,
    ILogger<IntegrationWebhookHostedService> logger) : BackgroundService
{
    private readonly WebhookDispatchOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Webhook dispatch polling is disabled");
            return;
        }

        var accessIdentity = new AccessIdentity(settings.Issuer, settings.Subject);
        var integrationIdentity = new IntegrationIdentity(settings.Issuer, settings.Subject);
        integrationIdentity.Validate();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!payloads.IsReady || !secrets.IsReady)
                {
                    health.RecordConfigurationBlock();
                    logger.LogWarning("Webhook dispatch is enabled but resolver providers are not ready");
                }
                else
                {
                    await RunCycle(accessIdentity, integrationIdentity, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error) when (error is AccessUnavailableException or IntegrationUnavailableException)
            {
                health.RecordFailure();
                logger.LogError("Webhook dispatch cycle failed because a required persistence dependency is unavailable");
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

    private async Task RunCycle(AccessIdentity accessIdentity, IntegrationIdentity integrationIdentity,
        CancellationToken cancellationToken)
    {
        Guid? after = null;
        var visited = 0;
        var delivered = 0;
        var retried = 0;
        var deadLettered = 0;
        var deferred = 0;

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
                visited++;
                try
                {
                    var result = await dispatcher.DispatchDueAsync(
                        integrationIdentity,
                        organization.Id,
                        settings.MaximumDeliveriesPerOrganization,
                        cancellationToken);
                    delivered += result.Delivered;
                    retried += result.Retried;
                    deadLettered += result.DeadLettered;
                    deferred += result.Deferred;
                }
                catch (IntegrationDeniedException)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Webhook dispatcher has no dispatch grant for organization {OrganizationId}",
                            organization.Id);
                    }
                }
                catch (IntegrationConflictException)
                {
                    health.RecordFailure();
                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning("Webhook delivery state changed concurrently for organization {OrganizationId}",
                            organization.Id);
                    }
                }
            }

            after = page.NextCursor;
        }
        while (after.HasValue);

        health.RecordCycle(visited, delivered, retried, deadLettered, deferred, timeProvider.GetUtcNow());
        if (delivered + retried + deadLettered + deferred > 0
            && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Webhook dispatch cycle processed {Organizations} organizations with {Delivered} delivered, {Retried} retryable, {DeadLettered} dead-lettered and {Deferred} deferred deliveries",
                visited, delivered, retried, deadLettered, deferred);
        }
    }
}
