using Avalonia.Controls;
using Avalonia.Input;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Management;
using SalekhPos.Desktop.Infrastructure.Authentication;
using SalekhPos.Desktop.ViewModels;

namespace SalekhPos.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly CashierViewModel viewModel;
    private readonly Action signOut;
    private readonly ICommerceExtensions? commerce;
    private readonly IAuditViewer? audit;
    private readonly IGlobalConfiguration? globalConfiguration;
    private readonly IAnalyticsViewer? analytics;
    private readonly IAccountingViewer? accounting;
    private readonly ISupportManager? support;
    private int signOutStarted;
    public MainWindow() => throw new InvalidOperationException("An authenticated workspace is required.");
    public MainWindow(IPosWorkspace workspace) : this(workspace, () => { })
    {
    }
    public MainWindow(IPosWorkspace workspace, Action signOut) : this(workspace, null, null, null, null, null, null, signOut) { }
    public MainWindow(IPosWorkspace workspace, ICommerceExtensions? commerce, Action signOut) : this(workspace, commerce, null, null, null, null, null, signOut) { }
    public MainWindow(IPosWorkspace workspace, ICommerceExtensions? commerce, IAuditViewer? audit, Action signOut) : this(workspace, commerce, audit, null, null, null, null, signOut) { }
    public MainWindow(IPosWorkspace workspace, ICommerceExtensions? commerce, IAuditViewer? audit,
        IGlobalConfiguration? globalConfiguration, IAnalyticsViewer? analytics, IAccountingViewer? accounting,
        Action signOut) : this(workspace, commerce, audit, globalConfiguration, analytics, accounting, null, signOut) { }
    public MainWindow(IPosWorkspace workspace, ICommerceExtensions? commerce, IAuditViewer? audit,
        IGlobalConfiguration? globalConfiguration, IAnalyticsViewer? analytics, IAccountingViewer? accounting,
        ISupportManager? support, Action signOut)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(signOut);
        this.signOut = signOut; this.commerce = commerce; this.audit = audit; this.globalConfiguration = globalConfiguration; this.analytics = analytics; this.accounting = accounting; this.support = support;
        InitializeComponent(); DataContext = viewModel = new(workspace);
        Opened += (_, _) => viewModel.InitializeFromPreparedState();
    }
    private async void AddBarcodeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Scan();
    private async void BarcodeKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) await Scan(); }
    private async Task Scan() { var value = BarcodeBox.Text ?? ""; await Execute(() => viewModel.ScanAsync(value, default)); BarcodeBox.Clear(); BarcodeBox.Focus(); }
    private async void CompleteSaleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.CompleteAsync(CashReceivedBox.Text ?? "", default));
    private async void SynchronizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.SynchronizeAsync(default));
    private async void OpenShiftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Execute(() => viewModel.OpenShiftAsync(OpenCurrencyBox.Text ?? "", OpeningBalanceBox.Text ?? "", default));
    private async void RecordMovementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kind = (MovementKindBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        await Execute(() => viewModel.RecordMovementAsync(kind, MovementAmountBox.Text ?? "", MovementReasonBox.Text ?? "", default));
    }
    private async void CloseShiftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Execute(() => viewModel.CloseShiftAsync(CountedCashBox.Text ?? "", default));
    private void ManagementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (commerce is null) return;
        var scope = viewModel.CurrentScope;
        new ManagerCommerceWindow(commerce, scope.OrganizationId, scope.BranchId).Show(this);
    }
    private void GlobalConfigurationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (globalConfiguration is null) return; var scope = viewModel.CurrentScope;
        new ManagerGlobalConfigurationWindow(globalConfiguration, scope.OrganizationId, scope.BranchId).Show(this);
    }
    private void AnalyticsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (analytics is null) return; var scope = viewModel.CurrentScope;
        new ManagerAnalyticsWindow(analytics, scope.OrganizationId, scope.BranchId).Show(this);
    }
    private void AccountingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (accounting is null) return; var scope = viewModel.CurrentScope;
        new ManagerAccountingWindow(accounting, scope.OrganizationId, scope.BranchId).Show(this);
    }
    private void SupportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (support is null) return; var scope = viewModel.CurrentScope;
        new ManagerSupportWindow(support, scope.OrganizationId, scope.BranchId).Show(this);
    }
    private void AuditClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (audit is null) return; var scope = viewModel.CurrentScope; new ManagerAuditWindow(audit, scope.OrganizationId, scope.BranchId).Show(this);
    }
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
