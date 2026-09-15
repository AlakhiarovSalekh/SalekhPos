using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using SalekhPos.Authorization.Application;
using SalekhPos.Catalog.Application.Products;
using SalekhPos.Identity.Application.Sessions;
using SalekhPos.Inventory.Application.Stock;
using SalekhPos.Pricing.Application.Prices;
using SalekhPos.Payments.Application.Payments;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Sales.Application.Carts;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Application.Voids;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.Stores.Application.Registers;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.Devices.Application.Devices;
using SalekhPos.Sync.Application.SyncMessages;
using SalekhPos.Customers.Application.Customers;
using SalekhPos.Suppliers.Application.Suppliers;
using SalekhPos.Purchasing.Application.PurchaseOrders;
using SalekhPos.Employees.Application.Employees;
using SalekhPos.Reporting.Application.Reports;
using SalekhPos.Audit.Application.AuditTrail;
using SalekhPos.Warehousing.Application.Transfers;
using SalekhPos.Promotions.Application.Campaigns;
using SalekhPos.Loyalty.Application.Accounts;

namespace SalekhPos.Api.ExceptionHandling;

public sealed partial class SafeExceptionHandler(IProblemDetailsService problems, ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            AccessDeniedException => (403, "access_denied", "Access is not permitted"),
            CatalogDeniedException => (403, "catalog_access_denied", "Catalog access is not permitted"),
            ProductConflictException => (409, "product_operation_conflict", "The product operation conflicts with current state"),
            ProductNotFoundException => (404, "product_not_found", "The product is unavailable"),
            InventoryDeniedException => (403, "inventory_access_denied", "Inventory access is not permitted"),
            InventoryConflictException => (409, "inventory_operation_conflict", "The inventory operation conflicts with current state"),
            PricingDeniedException => (403, "pricing_access_denied", "Pricing access is not permitted"),
            PricingConflictException => (409, "pricing_operation_conflict", "The pricing operation conflicts with current state"),
            SalesDeniedException or CartDeniedException => (403, "sales_access_denied", "Sales access is not permitted"),
            SaleVoidDeniedException => (403, "sale_void_access_denied", "Sale void access is not permitted"),
            CartConflictException => (409, "cart_operation_conflict", "The suspended cart operation conflicts with current state"),
            SaleVoidConflictException => (409, "sale_void_conflict", "The sale cannot be voided in its current state"),
            SaleVoidNotFoundException => (404, "sale_void_sale_not_found", "The sale is unavailable"),
            PaymentDeniedException => (403, "payment_access_denied", "Payment access is not permitted"),
            ReturnDeniedException => (403, "return_access_denied", "Return access is not permitted"),
            ReturnConflictException => (409, "return_operation_conflict", "The return operation conflicts with current state"),
            ReturnSaleNotFoundException => (404, "return_sale_not_found", "The original sale is unavailable"),
            StoreDeniedException => (403, "store_access_denied", "Store access is not permitted"),
            StoreConflictException => (409, "register_operation_conflict", "The register operation conflicts with current state"),
            ShiftDeniedException => (403, "shift_access_denied", "Shift access is not permitted"),
            DeviceDeniedException => (403, "device_access_denied", "Device access is not permitted"),
            DeviceProofException => (400, "invalid_device_proof", "The device proof is invalid"),
            DeviceRequestAuthenticationException or SyncRequestAuthenticationException or ShiftRequestAuthenticationException => (401, "invalid_device_request_proof", "The device request proof is invalid"),
            DeviceConflictException => (409, "device_operation_conflict", "The device operation conflicts with current state"),
            SyncDeniedException => (403, "sync_access_denied", "Synchronization access is not permitted"),
            SyncConflictException => (409, "sync_operation_conflict", "The synchronization message conflicts with current state"),
            ShiftConflictException => (409, "shift_operation_conflict", "The shift operation conflicts with current state"),
            CustomerDeniedException => (403, "customer_access_denied", "Customer access is not permitted"),
            CustomerConflictException => (409, "customer_operation_conflict", "The customer operation conflicts with current state"),
            CustomerNotFoundException => (404, "customer_not_found", "The customer is unavailable"),
            SupplierDeniedException => (403, "supplier_access_denied", "Supplier access is not permitted"),
            SupplierConflictException => (409, "supplier_operation_conflict", "The supplier operation conflicts with current state"),
            SupplierNotFoundException => (404, "supplier_not_found", "The supplier is unavailable"),
            PurchasingDeniedException => (403, "purchasing_access_denied", "Purchasing access is not permitted"),
            PurchaseOrderConflictException => (409, "purchase_order_conflict", "The purchase order conflicts with current state"),
            PurchaseOrderNotFoundException => (404, "purchase_order_not_found", "The purchase order is unavailable"),
            EmployeeDeniedException => (403, "employee_access_denied", "Employee access is not permitted"),
            EmployeeConflictException => (409, "employee_operation_conflict", "The employee operation conflicts with current state"),
            EmployeeNotFoundException => (404, "employee_not_found", "The employee is unavailable"),
            ReportingDeniedException => (403, "reporting_access_denied", "Reporting access is not permitted"),
            AuditDeniedException => (403, "audit_access_denied", "Audit access is not permitted"),
            AuditConflictException => (409, "audit_operation_conflict", "The audit operation conflicts with existing evidence"),
            WarehousingDeniedException => (403, "warehousing_access_denied", "Warehousing access is not permitted"),
            StockTransferConflictException => (409, "stock_transfer_conflict", "The stock transfer conflicts with current state"),
            StockTransferNotFoundException => (404, "stock_transfer_not_found", "The stock transfer is unavailable"),
            StockTransferInsufficientStockException => (409, "stock_transfer_insufficient_stock", "Available source stock is insufficient"),
            PromotionsDeniedException => (403, "promotions_access_denied", "Promotions access is not permitted"),
            PromotionConflictException => (409, "promotion_conflict", "The promotion conflicts with current state"),
            PromotionNotFoundException => (404, "promotion_not_found", "The promotion is unavailable"),
            LoyaltyDeniedException => (403, "loyalty_access_denied", "Loyalty access is not permitted"),
            LoyaltyConflictException => (409, "loyalty_conflict", "The loyalty operation conflicts with current state"),
            LoyaltyNotFoundException => (404, "loyalty_account_not_found", "The loyalty account is unavailable"),
            InsufficientLoyaltyPointsException => (409, "insufficient_loyalty_points", "The loyalty account has insufficient points"),
            SalesConflictException => (409, "sale_operation_conflict", "The sale operation conflicts with current state"),
            SalePriceUnavailableException => (409, "sale_price_unavailable", "A current product price is unavailable"),
            InsufficientStockException => (409, "insufficient_stock", "Available stock is insufficient"),
            PlatformAccessDeniedException => (403, "platform_access_denied", "Platform access is not permitted"),
            PlatformConflictException => (409, "platform_operation_conflict", "The platform operation conflicts with current state"),
            PlatformUnavailableException => (503, "platform_unavailable", "Platform administration is temporarily unavailable"),
            AccessUnavailableException or CatalogUnavailableException or InventoryUnavailableException or PricingUnavailableException or SalesUnavailableException or SalesCartUnavailableException or SaleVoidUnavailableException or PaymentsUnavailableException or ReturnsUnavailableException or StoreUnavailableException or ShiftUnavailableException or DeviceUnavailableException or SyncUnavailableException or IdentityUnavailableException or CustomerUnavailableException or SupplierUnavailableException or PurchasingUnavailableException or EmployeeUnavailableException or ReportingUnavailableException or WarehousingUnavailableException or PromotionsUnavailableException or LoyaltyUnavailableException or AuditUnavailableException or NpgsqlException or TimeoutException => (503, "service_unavailable", "The service is temporarily unavailable"),
            BadHttpRequestException bad => (bad.StatusCode, "invalid_request", "The request is invalid"),
            _ => (500, "internal_error", "The request could not be completed")
        };
        // Do not log exception messages/objects: they may contain SQL, connection
        // details, request data or secrets from downstream components.
        RequestFailed(logger, status, exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier }
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request failed with status {Status}, class {FailureClass}, trace {TraceId}")]
    private static partial void RequestFailed(ILogger logger, int status, string failureClass, string traceId);
}
