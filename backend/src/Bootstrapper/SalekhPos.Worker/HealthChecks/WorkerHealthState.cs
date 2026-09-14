namespace SalekhPos.Worker.HealthChecks;

public sealed class WorkerHealthState
{
    private readonly Lock _gate = new();
    private bool _started;
    private bool _stopping;
    private bool _stopped;
    private int _queued;
    private int _active;
    private long _succeeded;
    private long _failed;
    private long _timedOut;
    private long _cancelled;
    private long _retries;

    public WorkerHealthSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new WorkerHealthSnapshot(
                IsLive: !_stopped,
                IsReady: _started && !_stopping && !_stopped,
                IsAcceptingJobs: _started && !_stopping && !_stopped,
                QueuedJobs: _queued,
                ActiveJobs: _active,
                SucceededJobs: _succeeded,
                FailedJobs: _failed,
                TimedOutJobs: _timedOut,
                CancelledJobs: _cancelled,
                RetryAttempts: _retries);
        }
    }

    internal void Started()
    {
        lock (_gate)
        {
            _started = true;
        }
    }

    internal void BeginStopping()
    {
        lock (_gate)
        {
            _stopping = true;
        }
    }

    internal void Stopped()
    {
        lock (_gate)
        {
            _stopped = true;
        }
    }

    internal void JobQueued()
    {
        lock (_gate)
        {
            _queued++;
        }
    }

    internal void JobDequeued()
    {
        lock (_gate)
        {
            _queued--;
        }
    }

    internal void JobStarted()
    {
        lock (_gate)
        {
            _active++;
        }
    }

    internal void JobSucceeded()
    {
        lock (_gate)
        {
            _active--;
            _succeeded++;
        }
    }

    internal void JobFailed()
    {
        lock (_gate)
        {
            _active--;
            _failed++;
        }
    }

    internal void JobTimedOut()
    {
        lock (_gate)
        {
            _active--;
            _timedOut++;
        }
    }

    internal void JobCancelled()
    {
        lock (_gate)
        {
            _active--;
            _cancelled++;
        }
    }

    internal void JobRetried()
    {
        lock (_gate)
        {
            _retries++;
        }
    }
}

public sealed record WorkerHealthSnapshot(
    bool IsLive,
    bool IsReady,
    bool IsAcceptingJobs,
    int QueuedJobs,
    int ActiveJobs,
    long SucceededJobs,
    long FailedJobs,
    long TimedOutJobs,
    long CancelledJobs,
    long RetryAttempts);
