namespace SalekhPos.Worker.Jobs;

public sealed record BackgroundJobContext(
    Guid JobId,
    Guid OperationId,
    DateTimeOffset EnqueuedAtUtc,
    int Attempt);
