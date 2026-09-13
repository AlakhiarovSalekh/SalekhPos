using System.Net;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.ViewModels;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class PosReadinessSessionTests
{
    [Fact]
    public async Task TransientFailureFallsBackOfflineOnlyWithReadyProjectionAndCanRetryInitialSync()
    {
        var workspace = new Workspace
        {
            OpenOnline = _ => throw new HttpRequestException("Unavailable"),
            OpenOffline = _ => throw new CatalogProjectionNotReadyException(),
        };
        var publications = 0;
        var readiness = new PosReadinessSession(workspace, () => publications++);

        var first = await readiness.AttemptAsync(default);

        Assert.Equal(PosReadinessOutcome.InitialSyncRequired, first.Outcome);
        Assert.Null(first.State);
        Assert.Equal(0, publications);
        workspace.OpenOnline = _ => Task.FromResult(State(ready: true, online: true));

        var retry = await readiness.AttemptAsync(default);

        Assert.Equal(PosReadinessOutcome.ReadyOnline, retry.Outcome);
        Assert.Equal(1, publications);
    }

    [Fact]
    public async Task TransientFailurePublishesVerifiedOfflineWorkspace()
    {
        var workspace = new Workspace
        {
            OpenOnline = _ => throw new HttpRequestException("Unavailable", null,
                HttpStatusCode.ServiceUnavailable),
            OpenOffline = _ => Task.FromResult(State(ready: true, online: false)),
        };
        var publications = 0;

        var result = await new PosReadinessSession(workspace, () => publications++).AttemptAsync(default);

        Assert.Equal(PosReadinessOutcome.ReadyOffline, result.Outcome);
        Assert.Equal(1, publications);
        Assert.Equal(1, workspace.OfflineCalls);
    }

    [Fact]
    public async Task ReauthenticationAndNonTransientHttpFailureNeverEnterOfflineMode()
    {
        var reauthentication = new Workspace
        {
            OpenOnline = _ => throw new ReauthenticationRequiredException(),
        };
        var rejected = new Workspace
        {
            OpenOnline = _ => throw new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden),
        };

        await Assert.ThrowsAsync<ReauthenticationRequiredException>(() =>
            new PosReadinessSession(reauthentication, () => { }).AttemptAsync(default));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new PosReadinessSession(rejected, () => { }).AttemptAsync(default));

        Assert.Equal(0, reauthentication.OfflineCalls);
        Assert.Equal(0, rejected.OfflineCalls);
    }

    [Fact]
    public async Task FailureAndCancellationNeverPublishAndBothAllowRetry()
    {
        var attempts = 0;
        var workspace = new Workspace
        {
            OpenOnline = cancellationToken =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("Invalid response");
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(State(ready: true, online: true));
            },
        };
        var publications = 0;
        var readiness = new PosReadinessSession(workspace, () => publications++);

        await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.AttemptAsync(default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.AttemptAsync(cancelled.Token));
        Assert.Equal(0, publications);

        var retry = await readiness.AttemptAsync(default);

        Assert.Equal(PosReadinessOutcome.ReadyOnline, retry.Outcome);
        Assert.Equal(1, publications);
    }

    [Fact]
    public async Task ConcurrentOrRepeatedReadinessCannotPublishTwiceOrReopenWorkspace()
    {
        var release = new TaskCompletionSource<PosWorkspaceState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workspace = new Workspace { OpenOnline = _ => release.Task };
        var publications = 0;
        var readiness = new PosReadinessSession(workspace, () => publications++);
        var first = readiness.AttemptAsync(default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.AttemptAsync(default));
        release.SetResult(State(ready: true, online: true));
        Assert.Equal(PosReadinessOutcome.ReadyOnline, (await first).Outcome);

        var repeated = await readiness.AttemptAsync(default);

        Assert.Equal(PosReadinessOutcome.AlreadyPublished, repeated.Outcome);
        Assert.Equal(1, publications);
        Assert.Equal(1, workspace.OnlineCalls);
    }

    private static PosWorkspaceState State(bool ready, bool online) => new(
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), null, ready, 0, false, online,
        DateTimeOffset.UtcNow);

    private sealed class Workspace : IPosWorkspace
    {
        private PosWorkspaceState? currentState;
        public Func<CancellationToken, Task<PosWorkspaceState>> OpenOnline { get; set; } =
            _ => Task.FromResult(State(ready: true, online: true));
        public Func<CancellationToken, Task<PosWorkspaceState>> OpenOffline { get; set; } =
            _ => Task.FromResult(State(ready: false, online: false));
        public int OnlineCalls { get; private set; }
        public int OfflineCalls { get; private set; }
        public PosWorkspaceState CurrentState => currentState
            ?? throw new InvalidOperationException("Workspace is not open.");
        public async Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken)
        {
            OnlineCalls++;
            return currentState = await OpenOnline(cancellationToken);
        }
        public async Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken)
        {
            OfflineCalls++;
            return currentState = await OpenOffline(cancellationToken);
        }
        public Task<LocalSellableItem?> FindByProductAsync(Guid productId, DateTimeOffset at,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LocalSellableItem?> FindByBarcodeAsync(string barcode, DateTimeOffset at,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LocalSaleWriteResult> CompleteCashSaleAsync(CashCheckoutRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PosWorkspaceState> SynchronizeAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<CashMovementResult> RecordCashMovementAsync(Guid operationId, string kind, decimal amount,
            string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClosedCashSessionResult> CloseCashSessionAsync(Guid operationId, decimal countedCash,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
