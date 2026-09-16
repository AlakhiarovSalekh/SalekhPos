using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerAnalyticsWindow : Window
{
    private readonly IAnalyticsViewer analytics;
    private readonly Guid organizationId;
    private readonly Guid branchId;

    public ManagerAnalyticsWindow() => throw new InvalidOperationException("Analytics runtime is required.");
    public ManagerAnalyticsWindow(IAnalyticsViewer analytics, Guid organizationId, Guid branchId)
    {
        this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Analytics scope is invalid.");
        this.organizationId = organizationId;
        this.branchId = branchId;
        InitializeComponent();
        Opened += async (_, _) => await Load();
    }

    private async void RefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Load();

    private async Task Load()
    {
        var days = (WindowBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        { "1" => 1, "7" => 7, "90" => 90, _ => 30 };
        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-days);
        try
        {
            var overviewTask = analytics.ReadOverviewAsync(organizationId, branchId, from, to, default);
            var trendTask = analytics.ReadTrendAsync(organizationId, branchId, from, to, default);
            await Task.WhenAll(overviewTask, trendTask);
            var overview = await overviewTask; var trend = await trendTask;
            NetRevenueText.Text = Money(overview.NetRevenue, overview.Currency);
            SalesText.Text = overview.CompletedSales.ToString(System.Globalization.CultureInfo.InvariantCulture);
            AverageText.Text = Money(overview.AverageTicket, overview.Currency);
            ReturnsText.Text = $"{overview.CompletedReturns} · {Money(overview.Refunds, overview.Currency)}";
            StockText.Text = $"Positive {overview.PositiveStockProducts} · Zero {overview.ZeroStockProducts} · Negative {overview.NegativeStockProducts} · Sold {overview.DistinctProductsSold}";
            TrendList.ItemsSource = trend.Points.Select(point =>
                $"{point.BucketStart:yyyy-MM-dd} · {point.CompletedSales} sales · {Money(point.NetRevenue, point.Currency)} net").ToArray();
            try
            {
                var stores = await analytics.CompareStoresAsync(organizationId, from, to, default);
                StoresList.ItemsSource = stores.Items.Select(item =>
                    $"{item.BranchId:D} · {item.CompletedSales} sales · {Money(item.NetRevenue, item.Currency)} net · {Money(item.AverageTicket, item.Currency)} avg").ToArray();
            }
            catch (HttpRequestException) { StoresList.ItemsSource = new[] { "Organization-wide store comparison is unavailable for this session." }; }
            StatusText.Text = $"Loaded {days}-day analytics for branch {branchId:D}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            StatusText.Text = "Analytics could not be loaded.";
        }
    }

    private static string Money(decimal value, string? currency) =>
        currency is null ? value.ToString("0.00") : $"{value:0.00} {currency}";
}
