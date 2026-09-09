using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Contracts.Returns;

namespace SalekhPos.Returns.Api.Returns;

public static class ReturnEndpoints
{
    public static void MapReturnEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/returns",
            async (Guid organizationId, Guid branchId, CompleteReturnRequest request, HttpContext context,
                IReturnCompletion completion, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty) return Invalid();
                try
                {
                    var result = await completion.CompleteAsync(new(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value), new(organizationId, branchId, Guid.NewGuid(),
                        operationId, request.SaleId, request.Reason), cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/returns/{result.Return.Id:D}", result.Return)
                        : Results.Ok(result.Return);
                }
                catch (ArgumentException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The return request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_return_request" });
}
