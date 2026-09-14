using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalekhPos.Worker.HealthChecks;
using SalekhPos.Worker.Jobs;
using SalekhPos.Worker.Schedulers;

namespace SalekhPos.Worker.Dispatchers;

public sealed class BackgroundJobDispatcher(
    BackgroundJobQueue queue,
    IServiceScopeFactory scopeFactory,
    WorkerHealthState health,
    IOptions<WorkerOptions> options,
    IWorkerDelay delay,
    IWorkerRandom random,
    ILogger<BackgroundJobDispatcher> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _executionCancellation = new();
    private readonly WorkerOptions _options = options.Value;
    private Task[]? _consumers;
    private int _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_consumers is not null)
        {
            throw new InvalidOperationException("The background job dispatcher has already started.");
        }

        _consumers = [.. Enumerable.Range(0, _options.MaxConcurrency)
            .Select(_ => ConsumeAsync(_executionCancellation.Token))];
        health.Started();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Background job dispatcher started with concurrency {MaxConcurrency} and queue capacity {QueueCapacity}",
                _options.MaxConcurrency,
                _options.QueueCapacity);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        health.BeginStopping();
        queue.StopAccepting();
        logger.LogInformation("Background job dispatcher is draining queued and active jobs");

        var consumers = _consumers;
        if (consumers is not null)
        {
            using var drainCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drainCancellation.CancelAfter(_options.ShutdownDrainTimeout);
            try
            {
                await Task.WhenAll(consumers).WaitAsync(drainCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (drainCancellation.IsCancellationRequested)
            {
                logger.LogWarning("Background job drain deadline elapsed; cancelling active jobs");
                _executionCancellation.Cancel();
                await Task.WhenAll(consumers).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        health.Stopped();
        logger.LogInformation("Background job dispatcher stopped");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _executionCancellation.Cancel();
        _executionCancellation.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var queuedJob in queue.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteAsync(queuedJob, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ExecuteAsync(QueuedBackgroundJob queuedJob, CancellationToken shutdownToken)
    {
        health.JobStarted();

        for (var attempt = 1; attempt <= _options.RetryMaxAttempts; attempt++)
        {
            using var timeoutCancellation = new CancellationTokenSource(queuedJob.Timeout);
            using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                shutdownToken,
                timeoutCancellation.Token);
            using var loggingScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["JobId"] = queuedJob.Receipt.JobId,
                ["OperationId"] = queuedJob.Receipt.OperationId,
                ["JobType"] = queuedJob.JobType,
                ["Attempt"] = attempt,
            });

            try
            {
                logger.LogInformation("Background job execution started");
                var context = new BackgroundJobContext(
                    queuedJob.Receipt.JobId,
                    queuedJob.Receipt.OperationId,
                    queuedJob.Receipt.EnqueuedAtUtc,
                    attempt);
                await using var serviceScope = scopeFactory.CreateAsyncScope();
                await queuedJob.ExecuteAsync(serviceScope.ServiceProvider, context, executionCancellation.Token)
                    .ConfigureAwait(false);
                health.JobSucceeded();
                logger.LogInformation("Background job execution succeeded");
                return;
            }
            catch (RetryableBackgroundJobException) when (
                attempt < _options.RetryMaxAttempts &&
                !timeoutCancellation.IsCancellationRequested &&
                !shutdownToken.IsCancellationRequested)
            {
                health.JobRetried();
                var retryDelay = GetRetryDelay(attempt);
                var retryDelayMilliseconds = retryDelay.TotalMilliseconds;
                logger.LogWarning("Background job requested retry after {RetryDelayMs} ms", retryDelayMilliseconds);
                try
                {
                    await delay.DelayAsync(retryDelay, shutdownToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
                {
                    health.JobCancelled();
                    logger.LogWarning("Background job retry delay was cancelled during shutdown");
                    return;
                }
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                health.JobTimedOut();
                var timeoutMilliseconds = queuedJob.Timeout.TotalMilliseconds;
                logger.LogError("Background job execution timed out after {TimeoutMs} ms", timeoutMilliseconds);
                return;
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                health.JobCancelled();
                logger.LogWarning("Background job execution was cancelled during shutdown");
                return;
            }
            catch (Exception exception)
            {
                health.JobFailed();
                logger.LogError(
                    "Background job execution failed with failure type {FailureType}",
                    exception.GetType().FullName);
                return;
            }
        }
    }

    private TimeSpan GetRetryDelay(int failedAttempt)
    {
        var exponent = Math.Min(failedAttempt - 1, 30);
        var baseMilliseconds = _options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var boundedMilliseconds = Math.Min(baseMilliseconds, _options.RetryMaxDelay.TotalMilliseconds);
        var jitterMilliseconds = boundedMilliseconds * _options.RetryJitterRatio * random.NextDouble();
        return TimeSpan.FromMilliseconds(Math.Min(
            boundedMilliseconds + jitterMilliseconds,
            _options.RetryMaxDelay.TotalMilliseconds));
    }
}
