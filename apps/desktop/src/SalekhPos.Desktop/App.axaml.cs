using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SalekhPos.Desktop.Infrastructure.Configuration;

namespace SalekhPos.Desktop;

public sealed partial class App : Avalonia.Application
{
    private DesktopRuntimeBootstrap? bootstrap;
    private DesktopAuthenticatedRuntime? authenticatedRuntime;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, _) => DisposeRuntime();
            try
            {
                var settings = DesktopRuntimeSettings.FromEnvironment();
                bootstrap = DesktopRuntimeBootstrap.CreateDefault(settings);
                desktop.MainWindow = CreateSignInWindow(desktop);
            }
            catch (InvalidOperationException)
            {
                desktop.MainWindow = SignInWindow.ConfigurationLocked();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void CompleteSignIn(IClassicDesktopStyleApplicationLifetime desktop,
        DesktopAuthenticatedRuntime runtime)
    {
        authenticatedRuntime = runtime;
        var cashier = new MainWindow(runtime.Workspace, () => SignOut(desktop));
        var previous = desktop.MainWindow;
        desktop.MainWindow = cashier;
        cashier.Show();
        previous?.Close();
    }

    private SignInWindow CreateSignInWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var activeBootstrap = bootstrap
            ?? throw new InvalidOperationException("Desktop authentication is unavailable.");
        return new SignInWindow(activeBootstrap.SignInAsync, runtime => CompleteSignIn(desktop, runtime));
    }

    private void SignOut(IClassicDesktopStyleApplicationLifetime desktop)
    {
        authenticatedRuntime?.Dispose();
        authenticatedRuntime = null;
        var signIn = CreateSignInWindow(desktop);
        var previous = desktop.MainWindow;
        desktop.MainWindow = signIn;
        signIn.Show();
        previous?.Close();
    }

    private void DisposeRuntime()
    {
        authenticatedRuntime?.Dispose();
        bootstrap?.Dispose();
        authenticatedRuntime = null;
        bootstrap = null;
    }
}
