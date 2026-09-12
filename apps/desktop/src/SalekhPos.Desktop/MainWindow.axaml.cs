using Avalonia.Controls;
using Avalonia.Input;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.ViewModels;

namespace SalekhPos.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly CashierViewModel viewModel;
    public MainWindow() : this(null) { }
    public MainWindow(IPosWorkspace? workspace)
    {
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
    private static async Task Execute(Func<Task> action) { try { await action(); } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException) { } }
}
