using Avalonia.Controls;
using Avalonia.Input;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.ViewModels;

namespace SalekhPos.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly CashierViewModel viewModel;
    private readonly Action signOut;
    private int signOutStarted;
    public MainWindow() => throw new InvalidOperationException("An authenticated workspace is required.");
    public MainWindow(IPosWorkspace workspace) : this(workspace, () => { })
    {
    }
    public MainWindow(IPosWorkspace workspace, Action signOut)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(signOut);
        this.signOut = signOut;
        InitializeComponent(); DataContext = viewModel = new(workspace);
        Opened += async (_, _) => await Execute(() => viewModel.InitializeAsync(default));
    }
    private async void AddBarcodeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Scan();
    private async void BarcodeKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) await Scan(); }
    private async Task Scan() { var value = BarcodeBox.Text ?? ""; await Execute(() => viewModel.ScanAsync(value, default)); BarcodeBox.Clear(); BarcodeBox.Focus(); }
    private async void CompleteSaleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.CompleteAsync(CashReceivedBox.Text ?? "", default));
    private async void SynchronizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.SynchronizeAsync(default));
    private async void RecordMovementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kind = (MovementKindBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        await Execute(() => viewModel.RecordMovementAsync(kind, MovementAmountBox.Text ?? "", MovementReasonBox.Text ?? "", default));
    }
    private async void CloseShiftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.CloseShiftAsync(CountedCashBox.Text ?? "", default));
    private void SignOutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RequestSignOut();
    private async Task Execute(Func<Task> action)
    {
        try { await action(); }
        catch (ReauthenticationRequiredException) { RequestSignOut(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or HttpRequestException)
        {
        }
    }
    private void RequestSignOut()
    {
        if (Interlocked.Exchange(ref signOutStarted, 1) == 0) signOut();
    }
}
