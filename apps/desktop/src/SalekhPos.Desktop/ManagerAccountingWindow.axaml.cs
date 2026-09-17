using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerAccountingWindow : Window
{
    private readonly IAccountingViewer accounting;
    private readonly Guid organizationId;
    private readonly Guid branchId;
    private readonly List<AccountingJournalItem> journal = [];
    private DateTimeOffset currentFrom;
    private DateTimeOffset currentTo;
    private string? nextCursor;

    public ManagerAccountingWindow() => throw new InvalidOperationException("Accounting runtime is required.");
    public ManagerAccountingWindow(IAccountingViewer accounting, Guid organizationId, Guid branchId)
    {
        this.accounting = accounting ?? throw new ArgumentNullException(nameof(accounting));
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Accounting scope is invalid.");
        this.organizationId = organizationId;
        this.branchId = branchId;
        InitializeComponent();
        Opened += async (_, _) => await Load();
    }

    private async void RefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Load();
    private async void LoadMoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await LoadMore();
    private async Task Load()
    {
        var days = (WindowBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        { "1" => 1, "7" => 7, "90" => 90, _ => 30 };
        currentTo = DateTimeOffset.UtcNow;
        currentFrom = currentTo.AddDays(-days);
        try
        {
            var summaryTask = accounting.ReadSummaryAsync(organizationId, branchId, currentFrom, currentTo, default);
            var journalTask = accounting.ReadJournalAsync(organizationId, branchId, currentFrom, currentTo, 50, null, default);
            await Task.WhenAll(summaryTask, journalTask);
            var summary = await summaryTask;
            var page = await journalTask;
            NetReceiptsText.Text = Money(summary.NetReceipts, summary.Currency);
            GrossSalesText.Text = Money(summary.SalesGross, summary.Currency);
            TaxText.Text = Money(summary.SalesTax, summary.Currency);
            RefundsText.Text = Money(summary.Refunds, summary.Currency);
            CashMovementText.Text = Money(summary.CashIn - summary.CashOut, summary.Currency);
            PurchaseCommitmentsText.Text = $"{summary.ApprovedPurchaseOrders} · {Money(summary.PurchaseCommitments, summary.Currency)}";
            ShiftVarianceText.Text = $"{summary.ClosedShifts} · {Money(summary.ShiftVariance, summary.Currency)}";
            journal.Clear(); journal.AddRange(page.Items); nextCursor = page.NextCursor; RefreshJournal();
            StatusText.Text = $"Loaded {days}-day accounting evidence for branch {branchId:D}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            StatusText.Text = "Accounting data could not be loaded.";
        }
    }
    private async Task LoadMore()
    {
        if (nextCursor is null) return;
        try
        {
            var page = await accounting.ReadJournalAsync(organizationId, branchId,
                currentFrom, currentTo, 50, nextCursor, default);
            journal.AddRange(page.Items); nextCursor = page.NextCursor; RefreshJournal();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            StatusText.Text = "The next accounting journal page could not be loaded.";
        }
    }

    private void RefreshJournal()
    {
        JournalList.ItemsSource = journal.Select(item =>
            $"{item.OccurredAt:yyyy-MM-dd HH:mm} · {item.Kind.Replace('_', ' ')} · {Money(item.GrossAmount, item.Currency)} gross · {Money(item.CashEffect, item.Currency)} cash · {item.SourceId:D}").ToArray();
        LoadMoreButton.IsVisible = nextCursor is not null;
    }

    private static string Money(decimal value, string? currency) =>
        currency is null ? value.ToString("0.00") : $"{value:0.00} {currency}";
}
