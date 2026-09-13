using SalekhPos.Desktop.Application.POS;

namespace SalekhPos.Desktop.ViewModels;

public enum PosReadinessOutcome
{
    ReadyOnline,
    ReadyOffline,
    InitialSyncRequired,
    AlreadyPublished,
}

public sealed record PosReadinessResult(PosReadinessOutcome Outcome, PosWorkspaceState? State);

public sealed class PosReadinessSession
{
    private readonly IPosWorkspace workspace;
    private readonly Action publish;
    private int attemptRunning;
    private int published;

    public PosReadinessSession(IPosWorkspace workspace, Action publish)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(publish);
        this.workspace = workspace;
        this.publish = publish;
    }

    public async Task<PosReadinessResult> AttemptAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref published) != 0)
            return new(PosReadinessOutcome.AlreadyPublished, workspace.CurrentState);
        if (Interlocked.Exchange(ref attemptRunning, 1) != 0)
            throw new InvalidOperationException("A readiness attempt is already running.");

        try
        {
            PosWorkspaceState state;
            var offline = false;
            try
            {
                state = await workspace.OpenOnlineAsync(cancellationToken);
            }
            catch (HttpRequestException exception) when (IsTransient(exception))
            {
                try
                {
                    state = await workspace.OpenOfflineAsync(cancellationToken);
                }
                catch (CatalogProjectionNotReadyException)
                {
                    return new(PosReadinessOutcome.InitialSyncRequired, null);
                }
                offline = true;
            }

            if (!state.IsCatalogProjectionReady)
                return new(PosReadinessOutcome.InitialSyncRequired, state);

            publish();
            Volatile.Write(ref published, 1);
            return new(offline ? PosReadinessOutcome.ReadyOffline : PosReadinessOutcome.ReadyOnline, state);
        }
        finally
        {
            Volatile.Write(ref attemptRunning, 0);
        }
    }

    private static bool IsTransient(HttpRequestException exception) => exception.StatusCode is null
        or System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests
        || (int)exception.StatusCode >= 500;
}
