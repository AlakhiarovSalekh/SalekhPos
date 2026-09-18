namespace SalekhPos.Worker.Notifications;

public sealed record NotificationDispatchHealthSnapshot(
    long Cycles,
    long OrganizationsVisited,
    long Leased,
    long Delivered,
    long Retried,
    long DeadLettered,
    long ConfigurationBlocks,
    long Failures,
    DateTimeOffset? LastCompletedAt);

public sealed class NotificationDispatchHealthState
{
    private readonly System.Threading.Lock gate = new();
    private NotificationDispatchHealthSnapshot snapshot = new(0, 0, 0, 0, 0, 0, 0, 0, null);

    public NotificationDispatchHealthSnapshot Snapshot()
    {
        lock (gate)
        {
            return snapshot;
        }
    }

    public void RecordCycle(
        int organizationsVisited,
        int leased,
        int delivered,
        int retried,
        int deadLettered,
        DateTimeOffset completedAt)
    {
        if (organizationsVisited < 0 || leased < 0 || delivered < 0 || retried < 0 || deadLettered < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(organizationsVisited));
        }

        lock (gate)
        {
            snapshot = snapshot with
            {
                Cycles = snapshot.Cycles + 1,
                OrganizationsVisited = snapshot.OrganizationsVisited + organizationsVisited,
                Leased = snapshot.Leased + leased,
                Delivered = snapshot.Delivered + delivered,
                Retried = snapshot.Retried + retried,
                DeadLettered = snapshot.DeadLettered + deadLettered,
                LastCompletedAt = completedAt,
            };
        }
    }

    public void RecordConfigurationBlock()
    {
        lock (gate)
        {
            snapshot = snapshot with { ConfigurationBlocks = snapshot.ConfigurationBlocks + 1 };
        }
    }

    public void RecordFailure()
    {
        lock (gate)
        {
            snapshot = snapshot with { Failures = snapshot.Failures + 1 };
        }
    }
}
