using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Domain.LocalCatalog;

namespace SalekhPos.Desktop.ViewModels;

public sealed class CashierViewModel(IPosWorkspace? workspace) : INotifyPropertyChanged
{
    private bool busy;
    private bool opened;
    private string sessionStatus = "Sign-in and device setup are required.";
    private string syncStatus = "Not connected";
    private string message = "";
    private Guid saleId = Guid.NewGuid();
    private (Guid OperationId, string Kind, decimal Amount, string Reason)? pendingMovement;
    private (Guid OperationId, decimal CountedCash)? pendingClose;
    private (Guid OperationId, string Currency, decimal OpeningBalance)? pendingOpen;

    public ObservableCollection<CartLineViewModel> CartLines { get; } = [];
    public string SessionStatus { get => sessionStatus; private set => Set(ref sessionStatus, value); }
    public string SyncStatus { get => syncStatus; private set => Set(ref syncStatus, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public string GrandTotalText => GrandTotal.ToString("0.00", CultureInfo.InvariantCulture) + CurrencySuffix;
    public string CartStatus => CartLines.Count == 0 ? "Cart is empty" : $"{CartLines.Count} distinct item(s)";
    public bool IsReady => workspace is not null && opened && !busy;
    public bool IsOnlineSession => IsReady && workspace!.CurrentState.IsOnline && workspace.CurrentState.CanSell;
    public bool CanOpenShift => IsReady && workspace!.CurrentState.IsOnline
        && workspace.CurrentState.IsCatalogProjectionReady && workspace.CurrentState.CashSession is null;
    public bool CanCompleteSale => IsReady && workspace!.CurrentState.CanSell && CartLines.Count != 0;
    private decimal GrandTotal => CartLines.Sum(x => x.Total);
    private string CurrencySuffix => CartLines.Count == 0 ? "" : " " + CartLines[0].Currency;

    public void InitializeFromPreparedState()
    {
        if (workspace is null) return;
        Apply(workspace.CurrentState);
    }

    public async Task ScanAsync(string barcode, CancellationToken cancellationToken)
    {
        if (workspace is null || string.IsNullOrWhiteSpace(barcode)) return;
        await Run(async () =>
        {
            var item = await workspace.FindByBarcodeAsync(barcode.Trim(), DateTimeOffset.UtcNow, cancellationToken)
                ?? throw new InvalidOperationException("No sellable product matches the barcode.");
            Add(item);
        });
    }

    public async Task CompleteAsync(string cashReceived, CancellationToken cancellationToken)
    {
        if (workspace is null) return;
        if (!decimal.TryParse(cashReceived, NumberStyles.Number, CultureInfo.InvariantCulture, out var cash))
            throw new ArgumentException("Cash received must be a decimal number.");
        await Run(async () =>
        {
            var request = new CashCheckoutRequest(saleId, DateTimeOffset.UtcNow, cash,
                [.. CartLines.Select(x => new Application.Offline.ProjectedSaleItem(x.ProductId, x.Quantity))]);
            var result = await workspace.CompleteCashSaleAsync(request, cancellationToken);
            Message = result.Created ? $"Sale {result.Sale.SaleId:D} saved locally." : "Sale replay confirmed.";
            CartLines.Clear(); saleId = Guid.NewGuid(); ChangedCart(); Apply(workspace.CurrentState);
        });
    }

    public Task SynchronizeAsync(CancellationToken cancellationToken) => Run(async () =>
    {
        if (workspace is null) return;
        Apply(await workspace.SynchronizeAsync(cancellationToken)); Message = "Synchronization completed.";
    });

    public Task OpenShiftAsync(string currency, string openingBalance, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (workspace is null) return;
            var canonicalCurrency = (currency ?? "").Trim().ToUpperInvariant();
            if (canonicalCurrency.Length != 3 || canonicalCurrency.Any(c => c is < 'A' or > 'Z'))
                throw new ArgumentException("Currency must be exactly three ASCII letters.");
            const NumberStyles DecimalStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (!decimal.TryParse(openingBalance, DecimalStyle, CultureInfo.InvariantCulture, out var value)
                || value < 0 || decimal.Round(value, 6) != value)
                throw new ArgumentException("Opening balance must be a non-negative decimal with at most 6 decimal places.");
            if (pendingOpen is not null && (pendingOpen.Value.Currency != canonicalCurrency
                || pendingOpen.Value.OpeningBalance != value))
                throw new InvalidOperationException("Retry the unresolved shift opening with its original values.");
            pendingOpen ??= (Guid.NewGuid(), canonicalCurrency, value);
            await workspace.OpenCashSessionAsync(pendingOpen.Value.OperationId, canonicalCurrency, value,
                cancellationToken);
            pendingOpen = null;
            Apply(workspace.CurrentState); Message = "Cash shift opened.";
        });

    public Task RecordMovementAsync(string kind, string amount, string reason, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (workspace is null) return;
            if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("Movement amount must be a decimal number.");
            if (pendingMovement is not null
                && (pendingMovement.Value.Kind != kind || pendingMovement.Value.Amount != value
                    || pendingMovement.Value.Reason != reason))
                throw new InvalidOperationException("Retry the unresolved cash movement with its original values.");
            pendingMovement ??= (Guid.NewGuid(), kind, value, reason);
            await workspace.RecordCashMovementAsync(pendingMovement.Value.OperationId, kind, value, reason,
                cancellationToken);
            pendingMovement = null;
            Apply(workspace.CurrentState); Message = "Cash movement recorded.";
        });

    public Task CloseShiftAsync(string countedCash, CancellationToken cancellationToken) => Run(async () =>
    {
        if (workspace is null) return;
        if (!decimal.TryParse(countedCash, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Counted cash must be a decimal number.");
        if (pendingClose is not null && pendingClose.Value.CountedCash != value)
            throw new InvalidOperationException("Retry the unresolved shift close with the original counted cash.");
        pendingClose ??= (Guid.NewGuid(), value);
        var result = await workspace.CloseCashSessionAsync(pendingClose.Value.OperationId, value, cancellationToken);
        pendingClose = null;
        Apply(workspace.CurrentState); Message = $"Shift closed. Variance: {result.Variance:0.00} {result.Currency}.";
    });

    private void Add(LocalSellableItem item)
    {
        var existing = CartLines.SingleOrDefault(x => x.ProductId == item.ProductId);
        if (existing is null) CartLines.Add(new(item.ProductId, item.Sku, item.Name, 1m, item.UnitAmount, item.Currency));
        else existing.Increment();
        ChangedCart(); Message = "";
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) throw new InvalidOperationException("Another cashier operation is still running.");
        busy = true; ChangedAvailability();
        try { await action(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException)
        { Message = exception.Message; throw; }
        finally { busy = false; ChangedAvailability(); }
    }
    private void Apply(PosWorkspaceState state)
    {
        opened = true;
        SessionStatus = state.CashSession is null ? "No open cash session" :
            $"Shift {state.CashSession.ShiftId:D} · Register {state.CashSession.RegisterId:D}";
        SyncStatus = state.IsOnline ? $"Online · {state.PendingSales} pending" : $"Offline · {state.PendingSales} pending";
        ChangedAvailability();
    }
    private void ChangedCart() { Changed(nameof(GrandTotalText)); Changed(nameof(CartStatus)); Changed(nameof(CanCompleteSale)); }
    private void ChangedAvailability() { Changed(nameof(IsReady)); Changed(nameof(IsOnlineSession)); Changed(nameof(CanOpenShift)); Changed(nameof(CanCompleteSale)); }
    private void Set(ref string field, string value, [CallerMemberName] string? name = null) { if (field == value) return; field = value; Changed(name); }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CartLineViewModel(Guid productId, string sku, string name, decimal quantity, decimal unitAmount,
    string currency) : INotifyPropertyChanged
{
    public Guid ProductId { get; } = productId;
    public string Sku { get; } = sku;
    public string Name { get; } = name;
    public decimal Quantity { get; private set; } = quantity;
    public decimal UnitAmount { get; } = unitAmount;
    public string Currency { get; } = currency;
    public decimal Total => checked(Quantity * UnitAmount);
    public string QuantityText => $"{Quantity:0.######} × {UnitAmount:0.00}";
    public string TotalText => $"{Total:0.00} {Currency}";
    public void Increment() { Quantity += 1m; PropertyChanged?.Invoke(this, new(nameof(QuantityText))); PropertyChanged?.Invoke(this, new(nameof(TotalText))); }
    public event PropertyChangedEventHandler? PropertyChanged;
}
