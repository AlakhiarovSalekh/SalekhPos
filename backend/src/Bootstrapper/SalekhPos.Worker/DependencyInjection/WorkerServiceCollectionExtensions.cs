using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SalekhPos.Worker.Dispatchers;
using SalekhPos.Worker.HealthChecks;
using SalekhPos.Worker.Schedulers;
using SalekhPos.Worker.Integrations;
using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Infrastructure;
using SalekhPos.Integrations.Application;
using SalekhPos.Integrations.Infrastructure.Integrations;
using SalekhPos.Integrations.Infrastructure.Webhooks;

namespace SalekhPos.Worker.DependencyInjection;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<WebhookDispatchOptions>()
            .Bind(configuration.GetSection(WebhookDispatchOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton<IValidateOptions<WebhookDispatchOptions>, WebhookDispatchOptionsValidator>();
        services.TryAddSingleton(provider => new AccessDatabase(
            configuration.GetConnectionString("Application"),
            configuration.GetValue<bool>("Database:AllowLocalInsecureTransport")));
        services.TryAddSingleton<IAccessibleOrganizationReader, OrganizationAccessReader>();
        services.TryAddSingleton<IIntegrationService>(provider =>
            new PostgresIntegrationService(provider.GetRequiredService<AccessDatabase>().DataSource));
        services.TryAddSingleton<IWebhookHttpSender, SafeWebhookHttpSender>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookPayloadSource, PostgresWebhookPayloadSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookSecretSource, EnvironmentWebhookSecretSource>());
        services.TryAddSingleton<CompositeWebhookPayloadResolver>();
        services.TryAddSingleton<CompositeWebhookSecretResolver>();
        services.TryAddSingleton<IWebhookPayloadResolver>(provider =>
            provider.GetRequiredService<CompositeWebhookPayloadResolver>());
        services.TryAddSingleton<IWebhookSecretResolver>(provider =>
            provider.GetRequiredService<CompositeWebhookSecretResolver>());
        services.TryAddSingleton<WebhookTransport>();
        services.TryAddSingleton<WebhookTenantDispatcher>();
        services.TryAddSingleton<WebhookDispatchHealthState>();
        services.AddSingleton<IHostedService, IntegrationWebhookHostedService>();
        return services.AddWorkerRuntimeServices();
    }

    public static IServiceCollection AddWorkerRuntime(
        this IServiceCollection services,
        Action<WorkerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<WorkerOptions>()
            .Configure(configure)
            .ValidateOnStart();
        return services.AddWorkerRuntimeServices();
    }

    private static IServiceCollection AddWorkerRuntimeServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>();
        services.TryAddSingleton<IWorkerDelay, SystemWorkerDelay>();
        services.TryAddSingleton<IWorkerRandom, SystemWorkerRandom>();
        services.TryAddSingleton<WorkerHealthState>();
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton<IBackgroundJobQueue>(provider =>
            provider.GetRequiredService<BackgroundJobQueue>());
        services.TryAddSingleton<BackgroundJobDispatcher>();
        services.AddSingleton<IHostedService>(provider =>
            provider.GetRequiredService<BackgroundJobDispatcher>());
        return services;
    }
}
