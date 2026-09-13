using Avalonia.Controls;
using SalekhPos.Desktop.Infrastructure.Configuration;

namespace SalekhPos.Desktop;

public sealed partial class DeviceSetupWindow : Window
{
    private DesktopDeviceSetupSession? session;
    private readonly Action<DesktopAuthenticatedRuntime> completed;
    private readonly Action signOut;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? selection;
    private bool busy;

    public DeviceSetupWindow() => throw new InvalidOperationException("An authenticated setup session is required.");

    public DeviceSetupWindow(DesktopDeviceSetupSession session,
        Action<DesktopAuthenticatedRuntime> completed, Action signOut)
    {
        this.session = session;
        this.completed = completed;
        this.signOut = signOut;
        InitializeComponent();
        BranchBox.ItemsSource = session.Branches;
        if (session.Branches.Count == 1) BranchBox.SelectedIndex = 0;
        Closed += (_, _) => CloseResources();
    }

    private async void BranchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (session is null || BranchBox.SelectedItem is not DesktopBranch branch) return;
        selection?.Cancel();
        selection?.Dispose();
        selection = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var requestCancellation = selection;
        RegisterBox.ItemsSource = null;
        RegisterBox.IsEnabled = false;
        ProvisionButton.IsEnabled = false;
        Progress.IsVisible = true;
        StatusText.Text = "Loading active registers…";
        try
        {
            var registers = await session.GetActiveRegistersAsync(branch.Id, requestCancellation.Token);
            if (requestCancellation.IsCancellationRequested) return;
            RegisterBox.ItemsSource = registers;
            RegisterBox.IsEnabled = registers.Count > 0;
            if (registers.Count == 1) RegisterBox.SelectedIndex = 0;
            ProvisionButton.IsEnabled = registers.Count > 0;
            StatusText.Text = registers.Count == 0
                ? "No active registers are available for this branch."
                : "Select an active register and confirm this terminal's details.";
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            StatusText.Text = "Registers could not be loaded. Select the branch again to retry.";
        }
        finally
        {
            if (!requestCancellation.IsCancellationRequested) Progress.IsVisible = false;
        }
    }

    private async void ProvisionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (busy || session is null || BranchBox.SelectedItem is not DesktopBranch branch
            || RegisterBox.SelectedItem is not DesktopRegister register) return;
        busy = true;
        SetControls(false);
        Progress.IsVisible = true;
        StatusText.Text = "Provisioning and securing this terminal…";
        DesktopAuthenticatedRuntime? runtime = null;
        try
        {
            runtime = await session.ProvisionAsync(branch.Id, register.Id,
                CodeBox.Text ?? string.Empty, NameBox.Text ?? string.Empty, lifetime.Token);
            var transferred = session;
            session = null;
            try
            {
                completed(runtime);
                runtime = null;
            }
            finally
            {
                transferred.Dispose();
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (ArgumentException)
        {
            StatusText.Text = "Enter a valid terminal code and name, then try again.";
        }
        catch (Exception)
        {
            StatusText.Text = "Device setup could not be completed. Check the connection and try again.";
        }
        finally
        {
            runtime?.Dispose();
            busy = false;
            if (!lifetime.IsCancellationRequested && session is not null)
            {
                SetControls(true);
                Progress.IsVisible = false;
            }
        }
    }

    private void SignOutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => signOut();

    private void SetControls(bool enabled)
    {
        BranchBox.IsEnabled = enabled;
        RegisterBox.IsEnabled = enabled && RegisterBox.ItemCount > 0;
        CodeBox.IsEnabled = enabled;
        NameBox.IsEnabled = enabled;
        ProvisionButton.IsEnabled = enabled && RegisterBox.ItemCount > 0;
    }

    private void CloseResources()
    {
        lifetime.Cancel();
        selection?.Cancel();
        selection?.Dispose();
        session?.Dispose();
        session = null;
        lifetime.Dispose();
    }
}
