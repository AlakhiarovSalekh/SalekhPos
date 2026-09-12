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
                var signIn = new SignInWindow(
                    cancellationToken => bootstrap.SignInAsync(cancellationToken),
                    runtime => CompleteSignIn(desktop, runtime));
                desktop.MainWindow = signIn;
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
        var cashier = new MainWindow(runtime.Workspace);
        var previous = desktop.MainWindow;
        desktop.MainWindow = cashier;
        cashier.Show();
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
