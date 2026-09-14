namespace SalekhPos.Worker.Schedulers;

public interface IWorkerDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IWorkerRandom
{
    double NextDouble();
}

internal sealed class SystemWorkerDelay(TimeProvider timeProvider) : IWorkerDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);
}

internal sealed class SystemWorkerRandom : IWorkerRandom
{
    public double NextDouble() => Random.Shared.NextDouble();
}
