using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SalekhPos.Worker.Dispatchers;
using SalekhPos.Worker.HealthChecks;
using SalekhPos.Worker.Schedulers;

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
