using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SalekhPos.Worker.DependencyInjection;
using SalekhPos.Worker.Dispatchers;
using SalekhPos.Worker.HealthChecks;
using SalekhPos.Worker.Jobs;
using SalekhPos.Worker.Schedulers;
using Xunit;

namespace SalekhPos.Tests;

public sealed class WorkerRuntimeTests
{
    [Fact]
    public async Task BoundedQueueAppliesBackpressureAndHonorsProducerCancellation()
    {
        await using var provider = CreateProvider(options => options.QueueCapacity = 1);
        var queue = provider.GetRequiredService<IBackgroundJobQueue>();

        await queue.EnqueueAsync<NoOpJob, object>(new object(), BackgroundJobRequest.Create());
        using var cancellation = new CancellationTokenSource();
        var blockedWrite = queue.EnqueueAsync<NoOpJob, object>(
            new object(),
            BackgroundJobRequest.Create(),
            cancellation.Token).AsTask();

        Assert.False(blockedWrite.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blockedWrite);
        Assert.Equal(1, provider.GetRequiredService<WorkerHealthState>().GetSnapshot().QueuedJobs);
    }

    [Fact]
    public async Task PerJobTimeoutCancelsExecutionWithoutRetrying()
    {
        var payload = new CancellationPayload();
        await using var provider = CreateProvider(
            options =>
            {
                options.DefaultJobTimeout = TimeSpan.FromSeconds(10);
                options.MaximumJobTimeout = TimeSpan.FromSeconds(10);
            },
            services => services.AddSingleton<TimeoutJob>());
        var dispatcher = provider.GetRequiredService<BackgroundJobDispatcher>();
        await dispatcher.StartAsync(default);

        await provider.GetRequiredService<IBackgroundJobQueue>()
            .EnqueueAsync<TimeoutJob, CancellationPayload>(
                payload,
                BackgroundJobRequest.Create(TimeSpan.FromMilliseconds(40)));

        await payload.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => provider.GetRequiredService<WorkerHealthState>().GetSnapshot().TimedOutJobs == 1);
        var snapshot = provider.GetRequiredService<WorkerHealthState>().GetSnapshot();
        Assert.Equal(1, payload.Attempts);
        Assert.Equal(0, snapshot.RetryAttempts);
        await dispatcher.StopAsync(default);
    }

    [Fact]
    public async Task OnlyExplicitlyRetryableFailuresAreRetried()
    {
        var retryingPayload = new AttemptPayload(failuresBeforeSuccess: 2);
        var failingPayload = new AttemptPayload(failuresBeforeSuccess: int.MaxValue);
        var recordedDelay = new RecordingDelay();
        await using var provider = CreateProvider(
            options =>
            {
                options.RetryMaxAttempts = 3;
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(10);
                options.RetryMaxDelay = TimeSpan.FromMilliseconds(100);
                options.RetryJitterRatio = 0.5;
            },
            services =>
            {
                services.AddSingleton<RetryingJob>();
                services.AddSingleton<NonRetryingJob>();
                services.AddSingleton<IWorkerDelay>(recordedDelay);
                services.AddSingleton<IWorkerRandom>(new FixedRandom(0.5));
            });
        var dispatcher = provider.GetRequiredService<BackgroundJobDispatcher>();
        await dispatcher.StartAsync(default);
        var queue = provider.GetRequiredService<IBackgroundJobQueue>();

        await queue.EnqueueAsync<RetryingJob, AttemptPayload>(retryingPayload, BackgroundJobRequest.Create());
        await queue.EnqueueAsync<NonRetryingJob, AttemptPayload>(failingPayload, BackgroundJobRequest.Create());

        await WaitUntilAsync(() =>
        {
            var snapshot = provider.GetRequiredService<WorkerHealthState>().GetSnapshot();
            return snapshot.SucceededJobs == 1 && snapshot.FailedJobs == 1;
        });
        Assert.Equal(3, retryingPayload.Attempts);
        Assert.Equal(1, failingPayload.Attempts);
        Assert.Equal(2, recordedDelay.Delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(12.5), recordedDelay.Delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(25), recordedDelay.Delays[1]);
        await dispatcher.StopAsync(default);
    }

    [Fact]
    public async Task DispatcherNeverExceedsConfiguredConcurrency()
    {
        var payload = new ConcurrencyPayload(expectedFirstWave: 2);
        await using var provider = CreateProvider(
            options => options.MaxConcurrency = 2,
            services => services.AddSingleton<ConcurrencyJob>());
        var dispatcher = provider.GetRequiredService<BackgroundJobDispatcher>();
        await dispatcher.StartAsync(default);
        var queue = provider.GetRequiredService<IBackgroundJobQueue>();

        for (var index = 0; index < 4; index++)
        {
            await queue.EnqueueAsync<ConcurrencyJob, ConcurrencyPayload>(payload, BackgroundJobRequest.Create());
        }

        await payload.FirstWaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, payload.MaximumActive);
        payload.Release.TrySetResult();
        await WaitUntilAsync(() => provider.GetRequiredService<WorkerHealthState>().GetSnapshot().SucceededJobs == 4);
        Assert.Equal(2, payload.MaximumActive);
        await dispatcher.StopAsync(default);
    }

    [Fact]
    public async Task GracefulStopClosesReadinessRejectsNewWorkAndDrainsAcceptedJobs()
    {
        var payload = new GracefulPayload();
        await using var provider = CreateProvider(
            options =>
            {
                options.MaxConcurrency = 1;
                options.ShutdownDrainTimeout = TimeSpan.FromSeconds(2);
            },
            services => services.AddSingleton<GracefulJob>());
        var dispatcher = provider.GetRequiredService<BackgroundJobDispatcher>();
        var health = provider.GetRequiredService<WorkerHealthState>();
        await dispatcher.StartAsync(default);
        Assert.True(health.GetSnapshot().IsReady);
        var queue = provider.GetRequiredService<IBackgroundJobQueue>();
        await queue.EnqueueAsync<GracefulJob, GracefulPayload>(payload, BackgroundJobRequest.Create());
        await queue.EnqueueAsync<GracefulJob, GracefulPayload>(payload, BackgroundJobRequest.Create());
        await payload.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = dispatcher.StopAsync(default);
        await WaitUntilAsync(() => !health.GetSnapshot().IsReady);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await queue.EnqueueAsync<GracefulJob, GracefulPayload>(payload, BackgroundJobRequest.Create()));
        payload.Release.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2));

        var snapshot = health.GetSnapshot();
        Assert.False(snapshot.IsLive);
        Assert.False(snapshot.IsReady);
        Assert.Equal(2, snapshot.SucceededJobs);
        Assert.Equal(0, snapshot.QueuedJobs);
        Assert.Equal(0, snapshot.ActiveJobs);
    }

    [Fact]
    public async Task DrainDeadlineCancelsCancellationAwareActiveJob()
    {
        var payload = new CancellationPayload();
        await using var provider = CreateProvider(
            options =>
            {
                options.DefaultJobTimeout = TimeSpan.FromSeconds(10);
                options.MaximumJobTimeout = TimeSpan.FromSeconds(10);
                options.ShutdownDrainTimeout = TimeSpan.FromMilliseconds(40);
            },
            services => services.AddSingleton<TimeoutJob>());
        var dispatcher = provider.GetRequiredService<BackgroundJobDispatcher>();
        await dispatcher.StartAsync(default);
        await provider.GetRequiredService<IBackgroundJobQueue>()
            .EnqueueAsync<TimeoutJob, CancellationPayload>(payload, BackgroundJobRequest.Create());
        await payload.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await dispatcher.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(payload.Cancelled.Task.IsCompletedSuccessfully);
        var snapshot = provider.GetRequiredService<WorkerHealthState>().GetSnapshot();
        Assert.Equal(1, snapshot.CancelledJobs);
        Assert.False(snapshot.IsReady);
    }

    [Fact]
    public void InvalidConfigurationFailsWhenDispatcherIsResolved()
    {
        using var provider = CreateProvider(options => options.MaxConcurrency = 0);

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<BackgroundJobDispatcher>());
    }

    private static ServiceProvider CreateProvider(
        Action<WorkerOptions> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkerRuntime(configure);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class NoOpJob : IBackgroundJob<object>
    {
        public ValueTask ExecuteAsync(object payload, BackgroundJobContext context, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    private sealed class TimeoutJob : IBackgroundJob<CancellationPayload>
    {
        public async ValueTask ExecuteAsync(
            CancellationPayload payload,
            BackgroundJobContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref payload.Attempts);
            payload.Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                payload.Cancelled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class RetryingJob : IBackgroundJob<AttemptPayload>
    {
        public ValueTask ExecuteAsync(
            AttemptPayload payload,
            BackgroundJobContext context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref payload.Attempts) <= payload.FailuresBeforeSuccess)
            {
                throw new RetryableBackgroundJobException();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NonRetryingJob : IBackgroundJob<AttemptPayload>
    {
        public ValueTask ExecuteAsync(
            AttemptPayload payload,
            BackgroundJobContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref payload.Attempts);
            throw new InvalidOperationException("not retryable");
        }
    }

    private sealed class ConcurrencyJob : IBackgroundJob<ConcurrencyPayload>
    {
        public async ValueTask ExecuteAsync(
            ConcurrencyPayload payload,
            BackgroundJobContext context,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref payload.Active);
            UpdateMaximum(ref payload.MaximumActive, active);
            if (Interlocked.Increment(ref payload.Started) == payload.ExpectedFirstWave)
            {
                payload.FirstWaveStarted.TrySetResult();
            }

            await payload.Release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref payload.Active);
        }
    }

    private sealed class GracefulJob : IBackgroundJob<GracefulPayload>
    {
        public async ValueTask ExecuteAsync(
            GracefulPayload payload,
            BackgroundJobContext context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref payload.Started) == 1)
            {
                payload.FirstStarted.TrySetResult();
            }

            await payload.Release.Task.WaitAsync(cancellationToken);
        }
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var original = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (original == observed)
            {
                return;
            }

            observed = original;
        }
    }

    private sealed class RecordingDelay : IWorkerDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedRandom(double value) : IWorkerRandom
    {
        public double NextDouble() => value;
    }

    private sealed class AttemptPayload(int failuresBeforeSuccess)
    {
        public int FailuresBeforeSuccess { get; } = failuresBeforeSuccess;

        public int Attempts;
    }

    private sealed class CancellationPayload
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Attempts;
    }

    private sealed class ConcurrencyPayload(int expectedFirstWave)
    {
        public int ExpectedFirstWave { get; } = expectedFirstWave;

        public TaskCompletionSource FirstWaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Active;

        public int MaximumActive;

        public int Started;
    }

    private sealed class GracefulPayload
    {
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Started;
    }
}
