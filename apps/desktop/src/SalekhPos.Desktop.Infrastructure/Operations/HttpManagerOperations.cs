using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Operations;

namespace SalekhPos.Desktop.Infrastructure.Operations;

public sealed class HttpManagerOperations(HttpClient client) : IManagerOperations
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RegisterPage> ListRegistersAsync(Guid organizationId, Guid branchId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/registers?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<RegisterPage>(path, cancellationToken);
        ValidateRegisters(result, branchId, pageSize);
        return result;
    }
    public async Task<ResolvedPrice?> ResolvePriceAsync(Guid organizationId, Guid branchId, Guid productId,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId);
        if (productId == Guid.Empty || at == default || at.Offset != TimeSpan.Zero)
            throw new ArgumentException("Price query is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/pricing/resolve?branchId={branchId:D}&productId={productId:D}&at={Uri.EscapeDataString(at.ToString("O", CultureInfo.InvariantCulture))}";
        var result = await GetOptionalAsync<ResolvedPrice>(path, cancellationToken);
        if (result is not null) ValidatePrice(result, productId, branchId, at);
        return result;
    }

    public async Task<OpenShiftSummary?> ReadOpenShiftAsync(Guid organizationId, Guid branchId, Guid registerId,
        CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId);
        if (registerId == Guid.Empty) throw new ArgumentException("Register ID is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open?registerId={registerId:D}";
        var result = await GetOptionalAsync<OpenShiftSummary>(path, cancellationToken);
        if (result is not null) ValidateOpenShift(result, branchId, registerId);
        return result;
    }
    public async Task<IReadOnlyList<CashMovementSummary>> ListCashMovementsAsync(Guid organizationId,
        Guid branchId, Guid shiftId, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId);
        if (shiftId == Guid.Empty) throw new ArgumentException("Shift ID is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/cash-movements";
        var result = await GetAsync<List<CashMovementSummary>>(path, cancellationToken);
        if (result.Count > 10_000 || result.Any(x => x.Id == Guid.Empty || x.ShiftId != shiftId
            || InvalidCurrency(x.Currency) || InvalidAmount(x.Amount) || x.RecordedAt.Offset != TimeSpan.Zero
            || InvalidText(x.Kind, 32) || InvalidText(x.Reason, 500) || InvalidText(x.RecordedBy, 512)))
            throw new InvalidOperationException("Cash movement response is invalid.");
        return result.AsReadOnly();
    }

    public async Task<ClosedShiftPage> ListClosedShiftsAsync(Guid organizationId, Guid branchId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/closed?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<ClosedShiftPage>(path, cancellationToken);
        ValidateClosedPage(result, branchId, pageSize);
        return result;
    }
    public async Task<ClosedShiftSummary?> ReadClosedShiftAsync(Guid organizationId, Guid branchId, Guid shiftId,
        CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId);
        if (shiftId == Guid.Empty) throw new ArgumentException("Shift ID is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}";
        var result = await GetOptionalAsync<ClosedShiftSummary>(path, cancellationToken);
        if (result is not null) ValidateClosedShift(result, branchId);
        return result;
    }

    public async Task<PaymentEventPage> ListPaymentEventsAsync(Guid organizationId, Guid branchId, int pageSize,
        string? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        if (after is { Length: > 256 } || after?.Any(char.IsControl) == true)
            throw new ArgumentException("Payment cursor is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/payment-events?pageSize={pageSize}";
        if (!string.IsNullOrEmpty(after)) path += $"&after={Uri.EscapeDataString(after)}";
        var result = await GetAsync<PaymentEventPage>(path, cancellationToken);
        ValidatePaymentEvents(result, branchId, pageSize);
        return result;
    }
    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("The server response is empty.");
    }

    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }

    private static void ValidateScope(Guid organizationId, Guid branchId)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Organization and branch are required.");
    }

    private static void ValidatePage(int pageSize)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }
    private static void ValidateRegisters(RegisterPage page, Guid branchId, int pageSize)
    {
        if (page.Items.Count > pageSize || page.Items.Any(x => x.Id == Guid.Empty || x.BranchId != branchId
            || InvalidText(x.Code, 32) || InvalidText(x.Name, 100) || x.CreatedAt.Offset != TimeSpan.Zero))
            throw new InvalidOperationException("Register response is invalid.");
        if (page.NextCursor.HasValue && (page.Items.Count == 0 || page.Items[^1].Id != page.NextCursor.Value))
            throw new InvalidOperationException("Register cursor is invalid.");
    }

    private static void ValidatePrice(ResolvedPrice price, Guid productId, Guid branchId, DateTimeOffset at)
    {
        if (price.PriceId == Guid.Empty || price.ProductId != productId
            || (price.BranchId.HasValue && price.BranchId.Value != branchId) || price.Amount <= 0
            || InvalidAmount(price.Amount) || InvalidCurrency(price.Currency)
            || price.TaxMode is not ("inclusive" or "exclusive") || price.TaxRate is < 0 or > 100
            || decimal.Round(price.TaxRate, 4) != price.TaxRate || price.ValidFrom.Offset != TimeSpan.Zero
            || price.ValidFrom > at || price.ValidUntil.HasValue && price.ValidUntil.Value.Offset != TimeSpan.Zero || price.ValidUntil <= at)
            throw new InvalidOperationException("Resolved price response is invalid.");
    }

    private static void ValidateOpenShift(OpenShiftSummary shift, Guid branchId, Guid registerId)
    {
        if (shift.Id == Guid.Empty || shift.BranchId != branchId || shift.RegisterId != registerId
            || shift.Status != "open" || InvalidCurrency(shift.Currency) || shift.OpeningBalance < 0
            || InvalidAmount(shift.OpeningBalance) || shift.OpenedAt.Offset != TimeSpan.Zero
            || InvalidText(shift.OpenedBy, 512))
            throw new InvalidOperationException("Open shift response is invalid.");
    }
    private static void ValidateClosedPage(ClosedShiftPage page, Guid branchId, int pageSize)
    {
        if (page.Items.Count > pageSize) throw new InvalidOperationException("Closed shift page is too large.");
        foreach (var item in page.Items) ValidateClosedShift(item, branchId);
        if (page.NextCursor.HasValue && (page.Items.Count == 0 || page.Items[^1].Id != page.NextCursor.Value))
            throw new InvalidOperationException("Closed shift cursor is invalid.");
    }

    private static void ValidateClosedShift(ClosedShiftSummary shift, Guid branchId)
    {
        if (shift.Id == Guid.Empty || shift.BranchId != branchId || shift.RegisterId == Guid.Empty
            || shift.Status != "closed" || InvalidCurrency(shift.Currency) || shift.OpeningBalance < 0
            || shift.CashSales < 0 || shift.CashRefunds < 0 || shift.CashIn < 0 || shift.CashOut < 0
            || shift.CountedCash < 0 || InvalidAmount(shift.OpeningBalance) || InvalidAmount(shift.CashSales)
            || InvalidAmount(shift.CashRefunds) || InvalidAmount(shift.CashIn) || InvalidAmount(shift.CashOut)
            || InvalidAmount(shift.ExpectedCash) || InvalidAmount(shift.CountedCash) || InvalidAmount(shift.Variance)
            || shift.OpenedAt.Offset != TimeSpan.Zero || shift.ClosedAt.Offset != TimeSpan.Zero
            || shift.ClosedAt < shift.OpenedAt || InvalidText(shift.OpenedBy, 512) || InvalidText(shift.ClosedBy, 512))
            throw new InvalidOperationException("Closed shift response is invalid.");
        if (shift.ExpectedCash != shift.OpeningBalance + shift.CashSales - shift.CashRefunds + shift.CashIn - shift.CashOut
            || shift.Variance != shift.CountedCash - shift.ExpectedCash)
            throw new InvalidOperationException("Closed shift reconciliation is invalid.");
    }
    private static void ValidatePaymentEvents(PaymentEventPage page, Guid branchId, int pageSize)
    {
        if (page.Items.Count > pageSize || page.NextCursor is { Length: > 256 }
            || page.NextCursor?.Any(char.IsControl) == true)
            throw new InvalidOperationException("Payment event page is invalid.");
        foreach (var item in page.Items)
        {
            if (item.Id == Guid.Empty || item.PaymentId == Guid.Empty || item.BranchId != branchId
                || item.SourceId == Guid.Empty || item.Kind is not ("capture" or "return_refund" or "void_refund")
                || InvalidText(item.Method, 32) || InvalidText(item.Status, 32) || InvalidCurrency(item.Currency)
                || item.Amount <= 0 || InvalidAmount(item.Amount) || item.CompletedAt.Offset != TimeSpan.Zero)
                throw new InvalidOperationException("Payment event response is invalid.");
        }
    }

    private static bool InvalidCurrency(string? value) => value is null || value.Length != 3
        || value.Any(c => c is < 'A' or > 'Z');
    private static bool InvalidAmount(decimal value) => decimal.Round(value, 6) != value;
    private static bool InvalidText(string? value, int maximum) => string.IsNullOrWhiteSpace(value)
        || value != value.Trim() || value.Length > maximum || value.Any(char.IsControl);
}
