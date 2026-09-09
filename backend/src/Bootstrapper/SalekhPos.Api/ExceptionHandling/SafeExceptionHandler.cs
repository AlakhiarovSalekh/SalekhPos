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
            SalesConflictException => (409, "sale_operation_conflict", "The sale operation conflicts with current state"),
            SalePriceUnavailableException => (409, "sale_price_unavailable", "A current product price is unavailable"),
            InsufficientStockException => (409, "insufficient_stock", "Available stock is insufficient"),
            PlatformAccessDeniedException => (403, "platform_access_denied", "Platform access is not permitted"),
            PlatformConflictException => (409, "platform_operation_conflict", "The platform operation conflicts with current state"),
            PlatformUnavailableException => (503, "platform_unavailable", "Platform administration is temporarily unavailable"),
            AccessUnavailableException or CatalogUnavailableException or InventoryUnavailableException or PricingUnavailableException or SalesUnavailableException or SalesCartUnavailableException or SaleVoidUnavailableException or PaymentsUnavailableException or ReturnsUnavailableException or StoreUnavailableException or IdentityUnavailableException or NpgsqlException or TimeoutException => (503, "service_unavailable", "The service is temporarily unavailable"),
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
