using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SalekhPos.Desktop.Infrastructure.Configuration;

namespace SalekhPos.Desktop;

public sealed partial class App : Avalonia.Application
{
    private DesktopRuntimeBootstrap? bootstrap;
    private IDesktopAuthenticatedFlow? authenticatedFlow;

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
        IDesktopAuthenticatedFlow flow)
    {
        if (flow is DesktopDeviceSetupSession setup)
        {
            authenticatedFlow = setup;
            var setupWindow = new DeviceSetupWindow(setup,
                runtime => CompleteSetup(desktop, setup, runtime), () => SignOut(desktop));
            var previousSignIn = desktop.MainWindow;
            desktop.MainWindow = setupWindow;
            setupWindow.Show();
            previousSignIn?.Close();
            return;
        }
        var runtime = flow as DesktopAuthenticatedRuntime
            ?? throw new InvalidOperationException("The authenticated desktop flow is invalid.");
        BeginReadiness(desktop, runtime);
    }

    private void CompleteSetup(IClassicDesktopStyleApplicationLifetime desktop,
        DesktopDeviceSetupSession setup, DesktopAuthenticatedRuntime runtime)
    {
        if (!ReferenceEquals(authenticatedFlow, setup))
        {
            runtime.Dispose();
            return;
        }
        setup.Dispose();
        authenticatedFlow = null;
        BeginReadiness(desktop, runtime);
    }

    private void BeginReadiness(IClassicDesktopStyleApplicationLifetime desktop,
        DesktopAuthenticatedRuntime runtime)
    {
        var readiness = new ReadinessWindow(runtime,
            readyRuntime => CompleteReadiness(desktop, readyRuntime), () => SignOut(desktop));
        var previous = desktop.MainWindow;
        desktop.MainWindow = readiness;
        readiness.Show();
        previous?.Close();
    }

    private void CompleteReadiness(IClassicDesktopStyleApplicationLifetime desktop,
        DesktopAuthenticatedRuntime runtime)
    {
        var cashier = new MainWindow(runtime.Workspace, runtime.CommerceExtensions, runtime.AuditViewer, () => SignOut(desktop));
        authenticatedFlow = runtime;
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
        authenticatedFlow?.Dispose();
        authenticatedFlow = null;
        var signIn = CreateSignInWindow(desktop);
        var previous = desktop.MainWindow;
        desktop.MainWindow = signIn;
        signIn.Show();
        previous?.Close();
    }

    private void DisposeRuntime()
    {
        authenticatedFlow?.Dispose();
        bootstrap?.Dispose();
        authenticatedFlow = null;
        bootstrap = null;
    }
}
