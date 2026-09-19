using System.Globalization;
using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerPurchasingWindow : Window
{
    private readonly IManagerBusiness manager;
    private readonly Guid organizationId;
    private readonly Guid branchId;
    private PurchaseOrderSummary? receivingOrder;
    private PurchaseReceivingStateSummary? receivingState;
    private readonly List<ReceiptDisplay> receiptHistory = [];
    private Guid? receiptHistoryOrderId;
    private Guid? receiptHistoryCursor;
    private bool receiptHistoryBusy;

    public ManagerPurchasingWindow() =>
        throw new InvalidOperationException("Purchasing runtime is required.");

    public ManagerPurchasingWindow(
        IManagerBusiness manager,
        Guid organizationId,
        Guid branchId)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Organization and branch are required.");
        this.organizationId = organizationId;
        this.branchId = branchId;
        InitializeComponent();
        ScopeText.Text = $"Organization {organizationId:D} · Branch {branchId:D}";
        Opened += async (_, _) => await Refresh();
    }

    private async void RefreshClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e) => await Refresh();

    private async void LoadReceivingClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OrderList.SelectedItem is not OrderDisplay selected)
        {
            MessageText.Text = "Select a purchase order first.";
            return;
        }
        if (selected.Value.Status is not ("approved" or "partially_received"))
        {
            MessageText.Text = "Only approved or partially received orders can receive goods.";
            return;
        }

        await Execute(async () =>
        {
            var state = await manager.ReadPurchaseReceivingStateAsync(
                organizationId, branchId, selected.Value.Id, default);
            receivingOrder = selected.Value with { Status = state.Status, Version = state.Version };
            receivingState = state;
            ReceivingList.ItemsSource = state.Lines.Select(line => new ReceivingDisplay(line)).ToArray();
            StateText.Text = $"{state.Status} · version {state.Version} · " +
                $"{state.Lines.Count(line => line.RemainingQuantity > 0)} open line(s)";
        });
    }

    private async void LoadReceiptHistoryClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (receiptHistoryBusy)
        {
            MessageText.Text = "Receipt history is already loading.";
            return;
        }
        if (OrderList.SelectedItem is not OrderDisplay selected)
        {
            MessageText.Text = "Select a purchase order first.";
            return;
        }

        receiptHistoryOrderId = selected.Value.Id;
        receiptHistoryCursor = null;
        receiptHistory.Clear();
        ReceiptList.ItemsSource = Array.Empty<ReceiptDisplay>();
        ReceiptStateText.Text = "Loading receipt history…";
        await LoadReceiptPage(reset: true);
    }

    private async void LoadMoreReceiptsClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (receiptHistoryOrderId is null)
        {
            MessageText.Text = "Load receipt history for a purchase order first.";
            return;
        }
        if (receiptHistoryCursor is null)
        {
            MessageText.Text = "No more receipts are available.";
            return;
        }

        await LoadReceiptPage(reset: false);
    }

    private async Task LoadReceiptPage(bool reset)
    {
        if (receiptHistoryBusy) return;
        receiptHistoryBusy = true;
        var completed = false;
        try
        {
            await Execute(async () =>
            {
                if (receiptHistoryOrderId is null) return;
                var orderId = receiptHistoryOrderId.Value;
                var previousCursor = reset ? null : receiptHistoryCursor;
                var page = await manager.ListPurchaseReceiptsAsync(
                    organizationId, branchId, orderId, 25,
                    previousCursor, default);
                if (receiptHistoryOrderId != orderId) return;
                if (!reset && page.NextCursor == previousCursor)
                    throw new InvalidOperationException(
                        "Purchase receipt pagination repeated its cursor.");
                if (reset) receiptHistory.Clear();
                foreach (var receipt in page.Items)
                    if (receiptHistory.All(item => item.Value.Id != receipt.Id))
                        receiptHistory.Add(new(receipt));
                receiptHistoryCursor = page.NextCursor;
                UpdateReceiptList();
                completed = true;
            });
            if (reset && !completed)
                ReceiptStateText.Text = "Receipt history could not be loaded.";
        }
        finally
        {
            receiptHistoryBusy = false;
        }
    }

    private void UpdateReceiptList()
    {
        ReceiptList.ItemsSource = receiptHistory
            .OrderByDescending(item => item.Value.CreatedAt)
            .ToArray();
        ReceiptStateText.Text = receiptHistory.Count == 0
            ? "No receipts have been recorded for this purchase order."
            : $"{receiptHistory.Count} receipt(s) loaded" +
              (receiptHistoryCursor.HasValue ? " · more available" : "");
    }

    private void ReceivingSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (ReceivingList.SelectedItem is ReceivingDisplay selected)
            QuantityBox.Text = selected.Value.RemainingQuantity.ToString(
                CultureInfo.InvariantCulture);
    }

    private async void ReceiveClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (receivingOrder is null || receivingState is null
            || ReceivingList.SelectedItem is not ReceivingDisplay selected)
        {
            MessageText.Text = "Load a receivable order and select a line first.";
            return;
        }
        if (!decimal.TryParse(QuantityBox.Text, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var quantity)
            || quantity <= 0 || decimal.Round(quantity, 6) != quantity
            || quantity > selected.Value.RemainingQuantity)
        {
            MessageText.Text = "Receipt quantity must be positive and no greater than remaining.";
            return;
        }

        await Execute(async () =>
        {
            var receiptReference = ReferenceBox.Text?.Trim();
            var input = new ReceivePurchaseOrderInput(
                string.IsNullOrWhiteSpace(receiptReference) ? null : receiptReference,
                DateTimeOffset.UtcNow,
                [new(selected.Value.ProductId, quantity)]);
            var result = await manager.ReceivePurchaseOrderAsync(
                organizationId, branchId, receivingOrder, input, Guid.NewGuid(), default);
            if (receiptHistoryOrderId == result.Order.Id
                && receiptHistory.All(item => item.Value.Id != result.Receipt.Id))
            {
                receiptHistory.Add(new(result.Receipt));
                UpdateReceiptList();
            }
            MessageText.Text = $"Receipt {result.Receipt.Id:D} recorded. Order is {result.Order.Status}.";
            ReferenceBox.Clear();
            QuantityBox.Clear();
            receivingOrder = null;
            receivingState = null;
            ReceivingList.ItemsSource = Array.Empty<ReceivingDisplay>();
            StateText.Text = "";
            await Refresh();
        }, clearMessage: false);
    }

    private async Task Refresh() => await Execute(async () =>
    {
        var page = await manager.ListPurchaseOrdersAsync(
            organizationId, branchId, 100, null, default);
        OrderList.ItemsSource = page.Items.Select(order => new OrderDisplay(order)).ToArray();
    });

    private async Task Execute(Func<Task> action, bool clearMessage = true)
    {
        try
        {
            await action();
            if (clearMessage) MessageText.Text = "";
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            MessageText.Text = exception is HttpRequestException
                ? "Purchasing service request failed."
                : exception.Message;
        }
    }

    private sealed record OrderDisplay(PurchaseOrderSummary Value)
    {
        public override string ToString() =>
            $"{Value.Reference ?? Value.Id.ToString("D")[..8]} · {Value.Status} · " +
            $"{Value.Total.ToString("0.00", CultureInfo.InvariantCulture)} {Value.Currency}";
    }

    private sealed record ReceivingDisplay(PurchaseReceivingLineSummary Value)
    {
        public override string ToString() =>
            $"{Value.ProductId:D} · ordered {Value.OrderedQuantity} · " +
            $"received {Value.ReceivedQuantity} · remaining {Value.RemainingQuantity}";
    }

    private sealed record ReceiptDisplay(PurchaseReceiptSummary Value)
    {
        public override string ToString()
        {
            var quantity = Value.Lines.Sum(line => line.Quantity)
                .ToString("0.######", CultureInfo.InvariantCulture);
            return $"{Value.Reference ?? Value.Id.ToString("D")[..8]} · " +
                $"{Value.ReceivedAt.LocalDateTime:g} · {Value.Lines.Count} line(s) · {quantity} units";
        }
    }
}
