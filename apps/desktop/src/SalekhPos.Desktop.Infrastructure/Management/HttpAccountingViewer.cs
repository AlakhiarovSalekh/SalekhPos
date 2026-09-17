using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpAccountingViewer(HttpClient client) : IAccountingViewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedKinds =
        ["sale", "return", "sale_void", "cash_in", "cash_out", "purchase_commitment"];

    public async Task<AccountingSummary> ReadSummaryAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, from, to);
        var result = await Get<AccountingSummary>(
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/accounting/summary{Window(from, to)}",
            cancellationToken);
        ValidateSummary(result, branchId, from, to);
        return result;
    }

    public async Task<AccountingJournalPage> ReadJournalAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, int pageSize, string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, from, to);
        if (pageSize is < 1 or > 100 || (cursor is not null &&
            (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 256 || cursor.Any(char.IsControl))))
            throw new ArgumentException("Accounting journal query is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/accounting/journal{Window(from, to)}&pageSize={pageSize}";
        if (cursor is not null) path += $"&cursor={Uri.EscapeDataString(cursor)}";
        var result = await Get<AccountingJournalPage>(path, cancellationToken);
        if (result.Items.Count > pageSize || (result.NextCursor is not null &&
            (result.NextCursor.Length > 256 || result.NextCursor.Any(char.IsControl))))
            throw new InvalidOperationException("Accounting journal response is invalid.");
        foreach (var item in result.Items) ValidateJournalItem(item);
        return result;
    }

    private async Task<T> Get<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The accounting response is empty.");
    }

    private static void ValidateSummary(AccountingSummary value, Guid branchId,
        DateTimeOffset from, DateTimeOffset to)
    {
        if (value.BranchId != branchId || value.From != from || value.To != to
            || value.CompletedSales < 0 || value.SalesNet < 0 || value.SalesTax < 0
            || value.SalesGross < 0 || value.CompletedReturns < 0 || value.Refunds < 0
            || value.CashIn < 0 || value.CashOut < 0 || value.ApprovedPurchaseOrders < 0
            || value.PurchaseCommitments < 0 || value.ClosedShifts < 0 || !ValidCurrency(value.Currency))
            throw new InvalidOperationException("Accounting summary response is invalid.");
    }
    private static void ValidateJournalItem(AccountingJournalItem item)
    {
        if (item.SourceId == Guid.Empty || !AllowedKinds.Contains(item.Kind)
            || item.OccurredAt.Offset != TimeSpan.Zero || !ValidCurrency(item.Currency)
            || item.GrossAmount < 0)
            throw new InvalidOperationException("Accounting journal item is invalid.");
    }

    private static bool ValidCurrency(string? value) => value is null
        || (value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z'));

    private static void ValidateScope(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Accounting scope is invalid.");
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || from >= to
            || to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Accounting window is invalid.");
    }

    private static string Window(DateTimeOffset from, DateTimeOffset to) =>
        $"?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
}
