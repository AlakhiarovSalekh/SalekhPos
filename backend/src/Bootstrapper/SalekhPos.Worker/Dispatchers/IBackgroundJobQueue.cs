using SalekhPos.Worker.Jobs;

namespace SalekhPos.Worker.Dispatchers;

/// <summary>
/// Provides bounded, process-local dispatch. Module-owned work that requires durable delivery
/// must be persisted before it is submitted to this queue.
/// </summary>
public interface IBackgroundJobQueue
{
    ValueTask<BackgroundJobReceipt> EnqueueAsync<TJob, TPayload>(
        TPayload payload,
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default)
        where TJob : class, IBackgroundJob<TPayload>;
}
