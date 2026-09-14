namespace SalekhPos.Worker.Jobs;

public sealed record BackgroundJobRequest(
    Guid OperationId,
    TimeSpan? Timeout = null)
{
    public static BackgroundJobRequest Create(TimeSpan? timeout = null) => new(Guid.NewGuid(), timeout);
}

public sealed record BackgroundJobReceipt(
    Guid JobId,
    Guid OperationId,
    DateTimeOffset EnqueuedAtUtc);
