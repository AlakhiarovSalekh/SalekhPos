using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SalekhPos.Worker.HealthChecks;
using SalekhPos.Worker.Jobs;
using SalekhPos.Worker.Schedulers;

namespace SalekhPos.Worker.Dispatchers;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<QueuedBackgroundJob> _channel;
    private readonly WorkerHealthState _health;
    private readonly TimeProvider _timeProvider;
    private readonly WorkerOptions _options;
    private int _accepting = 1;

    public BackgroundJobQueue(
        IOptions<WorkerOptions> options,
        WorkerHealthState health,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _health = health;
        _timeProvider = timeProvider;
        _channel = Channel.CreateBounded<QueuedBackgroundJob>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public async ValueTask<BackgroundJobReceipt> EnqueueAsync<TJob, TPayload>(
        TPayload payload,
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TPayload>
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(request);

        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("An operation id is required.", nameof(request));
        }

        var timeout = request.Timeout ?? _options.DefaultJobTimeout;
        if (timeout <= TimeSpan.Zero || timeout > _options.MaximumJobTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Job timeout must be positive and no greater than {_options.MaximumJobTimeout}.");
        }

        if (Volatile.Read(ref _accepting) == 0)
        {
            throw new InvalidOperationException("The worker is no longer accepting jobs.");
        }

        var receipt = new BackgroundJobReceipt(Guid.NewGuid(), request.OperationId, _timeProvider.GetUtcNow());
        var queuedJob = new QueuedBackgroundJob(
            receipt,
            typeof(TJob).FullName ?? typeof(TJob).Name,
            timeout,
            async (provider, context, token) =>
                await provider.GetRequiredService<TJob>()
                    .ExecuteAsync(payload, context, token)
                    .ConfigureAwait(false));

        while (await _channel.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
        {
            if (Volatile.Read(ref _accepting) == 0)
            {
                throw new InvalidOperationException("The worker is no longer accepting jobs.");
            }

            _health.JobQueued();
            if (_channel.Writer.TryWrite(queuedJob))
            {
                return receipt;
            }

            _health.JobDequeued();
        }

        throw new InvalidOperationException("The worker is no longer accepting jobs.");
    }

    internal async IAsyncEnumerable<QueuedBackgroundJob> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var queuedJob))
            {
                _health.JobDequeued();
                yield return queuedJob;
            }
        }
    }

    internal void StopAccepting()
    {
        if (Interlocked.Exchange(ref _accepting, 0) == 1)
        {
            _channel.Writer.TryComplete();
        }
    }
}

internal sealed record QueuedBackgroundJob(
    BackgroundJobReceipt Receipt,
    string JobType,
    TimeSpan Timeout,
    Func<IServiceProvider, BackgroundJobContext, CancellationToken, ValueTask> ExecuteAsync);
