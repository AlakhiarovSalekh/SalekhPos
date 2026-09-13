using Avalonia.Controls;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.Infrastructure.Configuration;
using SalekhPos.Desktop.ViewModels;

namespace SalekhPos.Desktop;

public sealed partial class ReadinessWindow : Window
{
    private DesktopAuthenticatedRuntime? runtime;
    private readonly Action<DesktopAuthenticatedRuntime> completed;
    private readonly Action signOut;
    private readonly PosReadinessSession readiness;
    private readonly CancellationTokenSource lifetime = new();
    private int signOutStarted;

    public ReadinessWindow() => throw new InvalidOperationException("An authenticated runtime is required.");

    public ReadinessWindow(DesktopAuthenticatedRuntime runtime,
        Action<DesktopAuthenticatedRuntime> completed, Action signOut)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(signOut);
        this.runtime = runtime;
        this.completed = completed;
        this.signOut = signOut;
        readiness = new(runtime.Workspace, PublishRuntime);
        InitializeComponent();
        Opened += async (_, _) => await AttemptAsync();
        Closed += (_, _) => CloseResources();
    }

    private async void RetryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await AttemptAsync();

    private async Task AttemptAsync()
    {
        if (runtime is null || lifetime.IsCancellationRequested) return;
        RetryButton.IsEnabled = false;
        Progress.IsVisible = true;
        StatusText.Text = "Synchronizing pending sales, shift, and sellable catalog…";
        try
        {
            var result = await readiness.AttemptAsync(lifetime.Token);
            if (result.Outcome == PosReadinessOutcome.InitialSyncRequired)
                StatusText.Text = "This terminal has no verified local catalog yet. Connect to the server and retry initial sync before entering cashier mode.";
            else if (result.Outcome == PosReadinessOutcome.ReadyOffline)
                StatusText.Text = "The server is unavailable. Verified local data is ready for offline cashier mode.";
        }
        catch (ReauthenticationRequiredException)
        {
            RequestSignOut();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException exception) when (exception.Message == "A readiness attempt is already running.")
        {
        }
        catch (Exception)
        {
            StatusText.Text = "Initial synchronization could not be completed safely. Check the connection and retry.";
        }
        finally
        {
            if (!lifetime.IsCancellationRequested && runtime is not null)
            {
                RetryButton.IsEnabled = true;
                Progress.IsVisible = false;
            }
        }
    }

    private void PublishRuntime()
    {
        var ownedRuntime = runtime ?? throw new ObjectDisposedException(nameof(ReadinessWindow));
        runtime = null;
        try
        {
            completed(ownedRuntime);
        }
        catch
        {
            ownedRuntime.Dispose();
            throw;
        }
    }

    private void SignOutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RequestSignOut();

    private void RequestSignOut()
    {
        if (Interlocked.Exchange(ref signOutStarted, 1) == 0) signOut();
    }

    private void CloseResources()
    {
        lifetime.Cancel();
        runtime?.Dispose();
        runtime = null;
        lifetime.Dispose();
    }
}
