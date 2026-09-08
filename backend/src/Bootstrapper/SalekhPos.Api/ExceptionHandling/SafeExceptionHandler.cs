using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using SalekhPos.Authorization.Application;
using SalekhPos.Catalog.Application.Products;
using SalekhPos.Identity.Application.Sessions;
using SalekhPos.Inventory.Application.Stock;
using SalekhPos.SystemAdministration.Application;

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
            PlatformAccessDeniedException => (403, "platform_access_denied", "Platform access is not permitted"),
            PlatformConflictException => (409, "platform_operation_conflict", "The platform operation conflicts with current state"),
            PlatformUnavailableException => (503, "platform_unavailable", "Platform administration is temporarily unavailable"),
            AccessUnavailableException or CatalogUnavailableException or InventoryUnavailableException or IdentityUnavailableException or NpgsqlException or TimeoutException => (503, "service_unavailable", "The service is temporarily unavailable"),
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
