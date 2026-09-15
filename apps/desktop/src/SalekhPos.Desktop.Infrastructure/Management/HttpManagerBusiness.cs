using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpManagerBusiness(HttpClient client) : IManagerBusiness
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UuidPage<CustomerSummary>> ListCustomersAsync(Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/customers?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<UuidPage<CustomerSummary>>(path, cancellationToken);
        ValidateCustomerPage(result, pageSize);
        return result;
    }
    public async Task<CustomerSummary> CreateCustomerAsync(Guid organizationId, CreateCustomerInput input,
        Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId); ValidateCustomerInput(input);
        var result = await PostAsync<CustomerSummary>($"api/v1/organizations/{organizationId:D}/customers",
            new { input.Code, input.DisplayName, input.Email, input.Phone }, operationId, cancellationToken);
        ValidateCustomer(result);
        if (result.Code != input.Code || result.DisplayName != input.DisplayName.Trim())
            throw new InvalidOperationException("Customer response does not match the request.");
        return result;
    }

    public async Task<UuidPage<SupplierSummary>> ListSuppliersAsync(Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/suppliers?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<UuidPage<SupplierSummary>>(path, cancellationToken);
        ValidateSupplierPage(result, pageSize);
        return result;
    }
    public async Task<SupplierSummary> CreateSupplierAsync(Guid organizationId, CreateSupplierInput input,
        Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId); ValidateSupplierInput(input);
        var result = await PostAsync<SupplierSummary>($"api/v1/organizations/{organizationId:D}/suppliers",
            new { input.Code, input.Name, input.TaxId, input.Email, input.Phone }, operationId, cancellationToken);
        ValidateSupplier(result);
        if (result.Code != input.Code || result.Name != input.Name.Trim())
            throw new InvalidOperationException("Supplier response does not match the request.");
        return result;
    }

    public async Task<UuidPage<EmployeeSummary>> ListEmployeesAsync(Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/employees?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<UuidPage<EmployeeSummary>>(path, cancellationToken);
        ValidateEmployeePage(result, branchId, pageSize);
        return result;
    }
    public async Task<EmployeeSummary> CreateEmployeeAsync(Guid organizationId, Guid branchId,
        CreateEmployeeInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidateOperation(operationId); ValidateEmployeeInput(input);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/employees";
        var result = await PostAsync<EmployeeSummary>(path,
            new { input.Code, input.DisplayName, input.Email, input.Phone, input.JobTitle },
            operationId, cancellationToken);
        ValidateEmployee(result, branchId);
        if (result.Code != input.Code || result.DisplayName != input.DisplayName.Trim())
            throw new InvalidOperationException("Employee response does not match the request.");
        return result;
    }

    public async Task<UuidPage<PurchaseOrderSummary>> ListPurchaseOrdersAsync(Guid organizationId,
        Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/purchase-orders?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        var result = await GetAsync<UuidPage<PurchaseOrderSummary>>(path, cancellationToken);
        ValidatePurchasePage(result, branchId, pageSize);
        return result;
    }
    public async Task<PurchaseOrderSummary> CreatePurchaseOrderAsync(Guid organizationId, Guid branchId,
        CreatePurchaseOrderInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidateOperation(operationId); ValidatePurchaseInput(input);
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/purchase-orders";
        var body = new { input.SupplierId, input.Currency, input.Reference,
            Lines = input.Lines.Select(x => new { x.ProductId, x.Quantity, x.UnitCost }).ToArray() };
        var result = await PostAsync<PurchaseOrderSummary>(path, body, operationId, cancellationToken);
        ValidatePurchaseOrder(result, branchId);
        if (result.SupplierId != input.SupplierId || result.Currency != input.Currency)
            throw new InvalidOperationException("Purchase order response does not match the request.");
        return result;
    }

    public async Task<PurchaseOrderSummary> ChangePurchaseOrderStatusAsync(Guid organizationId, Guid branchId,
        PurchaseOrderSummary order, string action, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePurchaseOrder(order, branchId);
        if (action is not ("submit" or "approve" or "cancel")) throw new ArgumentException("Purchase action is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/purchase-orders/{order.Id:D}/{action}";
        var result = await PostAsync<PurchaseOrderSummary>(path, new { ExpectedVersion = order.Version }, null, cancellationToken);
        ValidatePurchaseOrder(result, branchId);
        if (result.Id != order.Id) throw new InvalidOperationException("Purchase order identity changed.");
        return result;
    }
    public async Task<OperationalReportSummary> ReadOperationalReportAsync(Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId);
        if (from == default || to == default || from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || from >= to
            || to - from > TimeSpan.FromDays(366)) throw new ArgumentException("Report window is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/reports/operational-summary" +
            $"?from={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&to={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}";
        var result = await GetAsync<OperationalReportSummary>(path, cancellationToken);
        ValidateReport(result, organizationId, branchId, from, to);
        return result;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }

    private async Task<T> PostAsync<T>(string path, object body, Guid? operationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: JsonOptions) };
        if (operationId.HasValue) request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.Value.ToString("D"));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }
    private static void ValidateCustomerPage(UuidPage<CustomerSummary> page, int pageSize)
    {
        if (page.Items.Count > pageSize) throw new InvalidOperationException("Customer page is too large.");
        foreach (var item in page.Items) ValidateCustomer(item);
        ValidateCursor(page.Items.Select(x => x.Id).ToArray(), page.NextCursor);
    }

    private static void ValidateCustomer(CustomerSummary item)
    {
        if (item.Id == Guid.Empty || InvalidCode(item.Code) || InvalidText(item.DisplayName, 200)
            || InvalidOptionalText(item.Email, 254) || InvalidOptionalText(item.Phone, 32)
            || item.Version < 1 || item.CreatedAt.Offset != TimeSpan.Zero || item.UpdatedAt.Offset != TimeSpan.Zero
            || item.UpdatedAt < item.CreatedAt) throw new InvalidOperationException("Customer response is invalid.");
    }

    private static void ValidateSupplierPage(UuidPage<SupplierSummary> page, int pageSize)
    {
        if (page.Items.Count > pageSize) throw new InvalidOperationException("Supplier page is too large.");
        foreach (var item in page.Items) ValidateSupplier(item);
        ValidateCursor(page.Items.Select(x => x.Id).ToArray(), page.NextCursor);
    }

    private static void ValidateSupplier(SupplierSummary item)
    {
        if (item.Id == Guid.Empty || InvalidCode(item.Code) || InvalidText(item.Name, 200)
            || InvalidOptionalText(item.TaxId, 64) || InvalidOptionalText(item.Email, 254)
            || InvalidOptionalText(item.Phone, 32) || item.Version < 1 || item.CreatedAt.Offset != TimeSpan.Zero
            || item.UpdatedAt.Offset != TimeSpan.Zero || item.UpdatedAt < item.CreatedAt)
            throw new InvalidOperationException("Supplier response is invalid.");
    }
    private static void ValidateEmployeePage(UuidPage<EmployeeSummary> page, Guid branchId, int pageSize)
    {
        if (page.Items.Count > pageSize) throw new InvalidOperationException("Employee page is too large.");
        foreach (var item in page.Items) ValidateEmployee(item, branchId);
        ValidateCursor(page.Items.Select(x => x.Id).ToArray(), page.NextCursor);
    }

    private static void ValidateEmployee(EmployeeSummary item, Guid branchId)
    {
        if (item.Id == Guid.Empty || item.BranchId != branchId || InvalidCode(item.Code)
            || InvalidText(item.DisplayName, 200) || InvalidOptionalText(item.Email, 254)
            || InvalidOptionalText(item.Phone, 32) || InvalidText(item.JobTitle, 120) || item.Version < 1
            || item.CreatedAt.Offset != TimeSpan.Zero || item.UpdatedAt.Offset != TimeSpan.Zero
            || item.UpdatedAt < item.CreatedAt) throw new InvalidOperationException("Employee response is invalid.");
    }

    private static void ValidatePurchasePage(UuidPage<PurchaseOrderSummary> page, Guid branchId, int pageSize)
    {
        if (page.Items.Count > pageSize) throw new InvalidOperationException("Purchase order page is too large.");
        foreach (var item in page.Items) ValidatePurchaseOrder(item, branchId);
        ValidateCursor(page.Items.Select(x => x.Id).ToArray(), page.NextCursor);
    }

    private static void ValidatePurchaseOrder(PurchaseOrderSummary item, Guid branchId)
    {
        if (item.Id == Guid.Empty || item.BranchId != branchId || item.SupplierId == Guid.Empty
            || item.Status is not ("draft" or "submitted" or "approved" or "cancelled")
            || InvalidCurrency(item.Currency) || InvalidOptionalText(item.Reference, 120) || item.Total < 0
            || InvalidAmount(item.Total) || item.Version < 1 || item.CreatedAt.Offset != TimeSpan.Zero
            || item.UpdatedAt.Offset != TimeSpan.Zero || item.UpdatedAt < item.CreatedAt
            || item.Lines.Count is < 1 or > 500) throw new InvalidOperationException("Purchase order response is invalid.");
        foreach (var line in item.Lines)
        {
            if (line.ProductId == Guid.Empty || line.Quantity <= 0 || line.UnitCost < 0 || line.LineTotal < 0
                || InvalidAmount(line.Quantity) || InvalidAmount(line.UnitCost) || InvalidAmount(line.LineTotal)
                || line.LineTotal != line.Quantity * line.UnitCost)
                throw new InvalidOperationException("Purchase order line response is invalid.");
        }
        if (item.Total != item.Lines.Sum(line => line.LineTotal))
            throw new InvalidOperationException("Purchase order total is invalid.");
    }

    private static void ValidateReport(OperationalReportSummary item, Guid organizationId, Guid branchId,
        DateTimeOffset from, DateTimeOffset to)
    {
        if (item.OrganizationId != organizationId || item.BranchId != branchId || item.From != from || item.To != to
            || item.Currency is not null && InvalidCurrency(item.Currency) || item.SalesCount < 0 || item.ReturnCount < 0
            || item.PurchaseOrderCount < 0 || item.OpenShiftCount < 0 || item.SalesGross < 0 || item.ReturnsTotal < 0
            || item.PurchaseOrderTotal < 0 || InvalidAmount(item.SalesGross) || InvalidAmount(item.ReturnsTotal)
            || InvalidAmount(item.NetSales) || InvalidAmount(item.PurchaseOrderTotal)
            || item.NetSales != item.SalesGross - item.ReturnsTotal)
            throw new InvalidOperationException("Operational report response is invalid.");
    }
    private static void ValidatePurchaseInput(CreatePurchaseOrderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.SupplierId == Guid.Empty || InvalidCurrency(input.Currency)
            || InvalidOptionalText(input.Reference, 120) || input.Lines is null || input.Lines.Count is < 1 or > 500
            || input.Lines.Select(line => line.ProductId).Distinct().Count() != input.Lines.Count)
            throw new ArgumentException("Purchase order input is invalid.");
        foreach (var line in input.Lines)
        {
            if (line.ProductId == Guid.Empty || line.Quantity <= 0 || line.UnitCost < 0
                || InvalidAmount(line.Quantity) || InvalidAmount(line.UnitCost))
                throw new ArgumentException("Purchase order line is invalid.");
        }
    }

    private static void ValidateScope(Guid organizationId, Guid branchId)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Organization and branch are required.");
    }

    private static void ValidateOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
    }
    private static void ValidatePage(int pageSize)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static void ValidateOperation(Guid operationId)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
    }

    private static void ValidateCursor(IReadOnlyList<Guid> ids, Guid? nextCursor)
    {
        if (nextCursor.HasValue && (ids.Count == 0 || ids[^1] != nextCursor.Value))
            throw new InvalidOperationException("Management cursor is invalid.");
    }


    private static void ValidateCustomerInput(CreateCustomerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (InvalidCode(input.Code) || InvalidText(input.DisplayName, 160)
            || InvalidOptionalEmail(input.Email) || InvalidOptionalPhone(input.Phone, 32))
            throw new ArgumentException("Customer input is invalid.");
    }

    private static void ValidateSupplierInput(CreateSupplierInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (InvalidCode(input.Code) || InvalidText(input.Name, 180) || InvalidOptionalText(input.TaxId, 64)
            || InvalidOptionalEmail(input.Email) || InvalidOptionalPhone(input.Phone, 32))
            throw new ArgumentException("Supplier input is invalid.");
    }

    private static void ValidateEmployeeInput(CreateEmployeeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Code != input.Code.Trim().ToUpperInvariant() || input.Code.Length is < 1 or > 32
            || input.Code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            || InvalidText(input.DisplayName, 160) || InvalidOptionalEmail(input.Email)
            || InvalidOptionalText(input.Phone, 40) || InvalidText(input.JobTitle, 100))
            throw new ArgumentException("Employee input is invalid.");
    }

    private static bool InvalidOptionalEmail(string? value)
    {
        if (value is null) return false;
        if (InvalidOptionalText(value, 254) || value.Contains(' ')) return true;
        var at = value.IndexOf('@');
        return at < 1 || at != value.LastIndexOf('@') || at == value.Length - 1;
    }

    private static bool InvalidOptionalPhone(string? value, int maximum) => value is not null
        && (InvalidOptionalText(value, maximum)
            || value.Any(c => !(char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')')));

    private static bool InvalidCode(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Length > 40 || value != value.Trim()
        || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));

    private static bool InvalidCurrency(string? value) => value is null || value.Length != 3
        || value.Any(character => character is < 'A' or > 'Z');

    private static bool InvalidAmount(decimal value) => decimal.Round(value, 6) != value;
    private static bool InvalidOptionalText(string? value, int maximum) => value is not null
        && (value != value.Trim() || value.Length > maximum || value.Any(char.IsControl));

    private static bool InvalidText(string? value, int maximum) => string.IsNullOrWhiteSpace(value)
        || value != value.Trim() || value.Length > maximum || value.Any(char.IsControl);
}
