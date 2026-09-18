namespace SalekhPos.Worker.Integrations;

public sealed class WebhookDispatchHealthState
{
    private long cycles;
    private long organizationsVisited;
    private long delivered;
    private long retried;
    private long deadLettered;
    private long failures;
    private long configurationBlocks;
    private long lastCompletedUnixSeconds;

    public void RecordCycle(int organizations, int succeeded, int retryCount, int deadLetterCount, DateTimeOffset completedAt)
    {
        Interlocked.Increment(ref cycles);
        Interlocked.Add(ref organizationsVisited, organizations);
        Interlocked.Add(ref delivered, succeeded);
        Interlocked.Add(ref retried, retryCount);
        Interlocked.Add(ref deadLettered, deadLetterCount);
        Interlocked.Exchange(ref lastCompletedUnixSeconds, completedAt.ToUnixTimeSeconds());
    }

    public void RecordFailure() => Interlocked.Increment(ref failures);

    public void RecordConfigurationBlock() => Interlocked.Increment(ref configurationBlocks);

    public WebhookDispatchHealthSnapshot Snapshot()
    {
        var timestamp = Interlocked.Read(ref lastCompletedUnixSeconds);
        return new(
            Interlocked.Read(ref cycles),
            Interlocked.Read(ref organizationsVisited),
            Interlocked.Read(ref delivered),
            Interlocked.Read(ref retried),
            Interlocked.Read(ref deadLettered),
            Interlocked.Read(ref failures),
            Interlocked.Read(ref configurationBlocks),
            timestamp == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(timestamp));
    }
}

public sealed record WebhookDispatchHealthSnapshot(
    long Cycles,
    long OrganizationsVisited,
    long Delivered,
    long Retried,
    long DeadLettered,
    long Failures,
    long ConfigurationBlocks,
    DateTimeOffset? LastCompletedAt);
