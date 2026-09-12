using Avalonia.Controls;
using SalekhPos.Desktop.Infrastructure.Configuration;

namespace SalekhPos.Desktop;

public sealed partial class SignInWindow : Window
{
    private readonly Func<CancellationToken, Task<DesktopAuthenticatedRuntime>>? signIn;
    private readonly Action<DesktopAuthenticatedRuntime>? authenticated;
    private readonly CancellationTokenSource cancellation = new();
    private bool busy;

    public SignInWindow(Func<CancellationToken, Task<DesktopAuthenticatedRuntime>> signIn,
        Action<DesktopAuthenticatedRuntime> authenticated)
    {
        this.signIn = signIn;
        this.authenticated = authenticated;
        InitializeComponent();
        Closed += (_, _) => cancellation.Cancel();
    }

    public SignInWindow()
    {
        InitializeComponent();
        StatusText.Text = "Runtime configuration is incomplete or unsafe. The application is locked.";
        SignInButton.IsEnabled = false;
        Closed += (_, _) => cancellation.Cancel();
    }

    public static SignInWindow ConfigurationLocked() => new();

    private async void SignInClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (busy || signIn is null || authenticated is null) return;
        busy = true;
        SignInButton.IsEnabled = false;
        Progress.IsVisible = true;
        StatusText.Text = "Complete sign-in in your system browser.";
        DesktopAuthenticatedRuntime? runtime = null;
        try
        {
            runtime = await signIn(cancellation.Token);
            authenticated(runtime);
            runtime = null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            StatusText.Text = "Sign-in could not be completed. Verify the runtime settings and try again.";
        }
        finally
        {
            runtime?.Dispose();
            busy = false;
            if (!cancellation.IsCancellationRequested)
            {
                SignInButton.IsEnabled = signIn is not null;
                Progress.IsVisible = false;
            }
        }
    }
}
