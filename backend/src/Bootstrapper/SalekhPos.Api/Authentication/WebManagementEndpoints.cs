using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Customers.Application.Customers;
using SalekhPos.Customers.Contracts.Customers;
using SalekhPos.Employees.Application.Employees;
using SalekhPos.Employees.Contracts.Employees;
using SalekhPos.Purchasing.Application.PurchaseOrders;
using SalekhPos.Purchasing.Contracts.PurchaseOrders;
using SalekhPos.Reporting.Application.Reports;
using SalekhPos.Reporting.Domain.SalesReports;
using SalekhPos.Suppliers.Application.Suppliers;
using SalekhPos.Suppliers.Contracts.Suppliers;

namespace SalekhPos.Api.Authentication;

public static class WebManagementEndpoints
{
    public static void MapWebManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff/api/v1").AllowAnonymous().RequireRateLimiting("business");
        MapCustomers(group);
        MapSuppliers(group);
        MapEmployees(group);
        MapPurchasing(group);
        MapReports(group);
    }
    private static void MapCustomers(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/customers", async (Guid organizationId,
            HttpContext context, WebAuthenticationState state, ICustomerDirectory customers,
            CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId) || !WebBusinessEndpoints.TryListQuery(context, out var size, out var after))
                return Invalid("invalid_customer_query");
            return Results.Ok(await customers.ListAsync(new(raw.Value.Issuer, raw.Value.Subject), organizationId,
                size, after, cancellationToken));
        });
        group.MapPost("/organizations/{organizationId:guid}/customers", async (Guid organizationId,
            CreateCustomerRequest request, HttpContext context, WebAuthenticationState state,
            IAntiforgery antiforgery, ICustomerDirectory customers, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId) || !await ValidMutation(context, state, antiforgery)
                || !TryOperationId(context, out var operationId)) return Invalid("invalid_customer_request");
            var result = await customers.CreateAsync(new(raw.Value.Issuer, raw.Value.Subject),
                new(organizationId, Guid.NewGuid(), operationId, request.Code, request.DisplayName,
                    request.Email, request.Phone), cancellationToken);
            return result.Created ? Results.Created($"/bff/api/v1/organizations/{organizationId:D}/customers/{result.Customer.Id:D}", result.Customer) : Results.Ok(result.Customer);
        });
    }
    private static void MapSuppliers(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/suppliers", async (Guid organizationId,
            HttpContext context, WebAuthenticationState state, ISupplierDirectory suppliers,
            CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId) || !WebBusinessEndpoints.TryListQuery(context, out var size, out var after))
                return Invalid("invalid_supplier_query");
            return Results.Ok(await suppliers.ListAsync(new(raw.Value.Issuer, raw.Value.Subject), organizationId,
                size, after, cancellationToken));
        });
        group.MapPost("/organizations/{organizationId:guid}/suppliers", async (Guid organizationId,
            CreateSupplierRequest request, HttpContext context, WebAuthenticationState state,
            IAntiforgery antiforgery, ISupplierDirectory suppliers, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId) || !await ValidMutation(context, state, antiforgery)
                || !TryOperationId(context, out var operationId)) return Invalid("invalid_supplier_request");
            var result = await suppliers.CreateAsync(new(raw.Value.Issuer, raw.Value.Subject),
                new(organizationId, Guid.NewGuid(), operationId, request.Code, request.Name,
                    request.TaxId, request.Email, request.Phone), cancellationToken);
            var location = $"/bff/api/v1/organizations/{organizationId:D}/suppliers/{result.Supplier.Id:D}";
            return result.Created ? Results.Created(location, result.Supplier) : Results.Ok(result.Supplier);
        });
    }
    private static void MapEmployees(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/employees", async (
            Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
            IEmployeeDirectory employees, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId)
                || !WebBusinessEndpoints.TryListQuery(context, out var size, out var after))
                return Invalid("invalid_employee_query");
            return Results.Ok(await employees.ListAsync(new(raw.Value.Issuer, raw.Value.Subject),
                organizationId, branchId, size, after, cancellationToken));
        });
        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/employees", async (
            Guid organizationId, Guid branchId, CreateEmployeeRequest request, HttpContext context,
            WebAuthenticationState state, IAntiforgery antiforgery, IEmployeeDirectory employees,
            CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId) || !await ValidMutation(context, state, antiforgery)
                || !TryOperationId(context, out var operationId)) return Invalid("invalid_employee_request");
            var result = await employees.CreateAsync(new(raw.Value.Issuer, raw.Value.Subject),
                new(organizationId, branchId, Guid.NewGuid(), operationId, request.Code,
                    request.DisplayName, request.Email, request.Phone, request.JobTitle), cancellationToken);
            var location = $"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/employees/{result.Employee.Id:D}";
            return result.Created ? Results.Created(location, result.Employee) : Results.Ok(result.Employee);
        });
    }
    private static void MapPurchasing(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/purchase-orders", async (
            Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
            IPurchaseOrderService orders, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId)
                || !WebBusinessEndpoints.TryListQuery(context, out var size, out var after))
                return Invalid("invalid_purchase_order_query");
            return Results.Ok(await orders.ListAsync(new(raw.Value.Issuer, raw.Value.Subject),
                organizationId, branchId, size, after, cancellationToken));
        });
        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/purchase-orders", async (
            Guid organizationId, Guid branchId, CreatePurchaseOrderRequest request, HttpContext context,
            WebAuthenticationState state, IAntiforgery antiforgery, IPurchaseOrderService orders,
            CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId) || !await ValidMutation(context, state, antiforgery)
                || !TryOperationId(context, out var operationId)) return Invalid("invalid_purchase_order_request");
            var lines = request.Lines.Select(line => new CreatePurchaseOrderLine(
                line.ProductId, line.Quantity, line.UnitCost)).ToArray();
            var result = await orders.CreateAsync(new(raw.Value.Issuer, raw.Value.Subject),
                new(organizationId, Guid.NewGuid(), operationId, branchId, request.SupplierId,
                    request.Currency, request.Reference, lines), cancellationToken);
            var location = $"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/purchase-orders/{result.Order.Id:D}";
            return result.Created ? Results.Created(location, result.Order) : Results.Ok(result.Order);
        });
        MapPurchaseStatus(group, "submit", "submitted");
        MapPurchaseStatus(group, "approve", "approved");
        MapPurchaseStatus(group, "cancel", "cancelled");
    }

    private static void MapPurchaseStatus(RouteGroupBuilder group, string action, string target)
    {
        group.MapPost($"/organizations/{{organizationId:guid}}/branches/{{branchId:guid}}/purchase-orders/{{orderId:guid}}/{action}", async (
            Guid organizationId, Guid branchId, Guid orderId, ChangePurchaseOrderStatusRequest request,
            HttpContext context, WebAuthenticationState state, IAntiforgery antiforgery,
            IPurchaseOrderService orders, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId, orderId) || !await ValidMutation(context, state, antiforgery))
                return Invalid("invalid_purchase_order_request");
            return Results.Ok(await orders.ChangeStatusAsync(new(raw.Value.Issuer, raw.Value.Subject),
                new(organizationId, branchId, orderId, target, request.ExpectedVersion), cancellationToken));
        });
    }
    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/reports/operational-summary", async (
            Guid organizationId, Guid branchId, HttpContext context, WebAuthenticationState state,
            IOperationalReportService reports, CancellationToken cancellationToken) =>
        {
            var raw = await RawIdentity(context, state);
            if (raw is null) return Unauthenticated(state);
            if (!ValidIds(organizationId, branchId)
                || context.Request.Query.Keys.Any(key => key is not "from" and not "to")
                || !DateTimeOffset.TryParse(context.Request.Query["from"], out var from)
                || !DateTimeOffset.TryParse(context.Request.Query["to"], out var to))
                return Invalid("invalid_report_query");
            ReportWindow window;
            try { window = new(from, to); }
            catch (ArgumentException) { return Invalid("invalid_report_query"); }
            return Results.Ok(await reports.ReadSummaryAsync(new(raw.Value.Issuer, raw.Value.Subject),
                organizationId, branchId, window, cancellationToken));
        });
    }
    private static async Task<(string Issuer, string Subject)?> RawIdentity(HttpContext context,
        WebAuthenticationState state)
    {
        if (state.Settings is null) return null;
        var result = await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if (!result.Succeeded || result.Principal is null) return null;
        var issuers = result.Principal.FindAll("iss").ToArray();
        var subjects = result.Principal.FindAll("sub").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1) return null;
        var issuer = issuers[0].Value;
        var subject = subjects[0].Value;
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl)) return null;
        return (issuer, subject);
    }

    private static Task<bool> ValidMutation(HttpContext context, WebAuthenticationState state,
        IAntiforgery antiforgery) => state.Settings is null
            ? Task.FromResult(false)
            : WebAuthentication.ValidateMutation(context, antiforgery, state.Settings);
    private static bool TryOperationId(HttpContext context, out Guid operationId) =>
        Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out operationId)
        && operationId != Guid.Empty;

    private static IResult Unauthenticated(WebAuthenticationState state) => state.Settings is null
        ? Results.Problem(statusCode: 503, title: "Web authentication is unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "web_authentication_unavailable" })
        : Results.Unauthorized();

    private static IResult Invalid(string code) => Results.Problem(statusCode: 400,
        title: "The management request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static bool ValidIds(params Guid[] values) => values.All(value => value != Guid.Empty);
}
