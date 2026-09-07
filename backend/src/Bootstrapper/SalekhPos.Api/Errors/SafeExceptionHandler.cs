using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using SalekhPos.Access.Application;
using SalekhPos.Identity.Application;

namespace SalekhPos.Api.Errors;

public sealed partial class SafeExceptionHandler(IProblemDetailsService problems, ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            AccessDeniedException => (403, "access_denied", "Access is not permitted"),
            AccessUnavailableException or IdentityUnavailableException or NpgsqlException or TimeoutException => (503, "service_unavailable", "The service is temporarily unavailable"),
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
