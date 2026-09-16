using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpAnalyticsViewer(HttpClient client) : IAnalyticsViewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnalyticsOverviewSummary> ReadOverviewAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, from, to);
        var result = await Get<AnalyticsOverviewSummary>(
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/analytics/overview{Window(from, to)}",
            cancellationToken);
        ValidateOverview(result, branchId, from, to);
        return result;
    }

    public async Task<AnalyticsTrendSummary> ReadTrendAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, from, to);
        var result = await Get<AnalyticsTrendSummary>(
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/analytics/sales-trend{Window(from, to)}",
            cancellationToken);
        if (result.BranchId != branchId || result.From != from || result.To != to || result.Points.Count > 366
            || result.Points.Any(point => point.BucketStart.Offset != TimeSpan.Zero
                || point.CompletedSales < 0 || point.GrossSales < 0 || point.Refunds < 0))
            throw new InvalidOperationException("Analytics trend response is invalid.");
        return result;
    }

    public async Task<StoreComparisonResult> CompareStoresAsync(Guid organizationId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateWindow(from, to);
        var result = await Get<StoreComparisonResult>(
            $"api/v1/organizations/{organizationId:D}/analytics/stores{Window(from, to)}", cancellationToken);
        if (result.From != from || result.To != to || result.Items.Count > 500
            || result.Items.Any(item => item.BranchId == Guid.Empty || item.CompletedSales < 0
                || item.GrossSales < 0 || item.Refunds < 0 || item.AverageTicket < 0))
            throw new InvalidOperationException("Store comparison response is invalid.");
        return result;
    }

    private async Task<T> Get<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The analytics response is empty.");
    }
    private static void ValidateOverview(AnalyticsOverviewSummary value, Guid branchId,
        DateTimeOffset from, DateTimeOffset to)
    {
        if (value.BranchId != branchId || value.From != from || value.To != to
            || value.CompletedSales < 0 || value.GrossSales < 0 || value.CompletedReturns < 0
            || value.Refunds < 0 || value.AverageTicket < 0 || value.DistinctProductsSold < 0
            || value.PositiveStockProducts < 0 || value.ZeroStockProducts < 0 || value.NegativeStockProducts < 0)
            throw new InvalidOperationException("Analytics overview response is invalid.");
    }

    private static void ValidateScope(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to)
    {
        ValidateOrganization(organizationId);
        if (branchId == Guid.Empty) throw new ArgumentException("Branch is required.");
        ValidateWindow(from, to);
    }
    private static void ValidateOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
    }
    private static void ValidateWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || from >= to || to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Analytics window is invalid.");
    }
    private static string Window(DateTimeOffset from, DateTimeOffset to) =>
        $"?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
}
