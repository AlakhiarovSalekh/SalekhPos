using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Contracts.CompleteSale;

namespace SalekhPos.Sales.Api.CompleteSale;

public static class CashSaleEndpoints
{
    public static void MapCashSaleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context,
                ISaleReader reader, CancellationToken cancellationToken) =>
            {
                try
                {
                    var sale = await reader.ReadAsync(Identity(context), organizationId, branchId, saleId,
                        cancellationToken);
                    return sale is null ? Results.NotFound() : Results.Ok(sale);
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/cash",
            async (Guid organizationId, Guid branchId, CompleteCashSaleRequest request, HttpContext context,
                ICashSaleCompletion completion, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty || request.Lines is null) return Invalid();
                try
                {
                    var command = new CompleteCashSaleCommand(organizationId, branchId, Guid.NewGuid(), operationId,
                        request.Lines,
                        request.CashReceived);
                    var result = await completion.CompleteAsync(Identity(context), command, cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/sales/{result.Sale.Id:D}", result.Sale)
                        : Results.Ok(result.Sale);
                }
                catch (ArgumentException) { return Invalid(); }
                catch (OverflowException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }

    private static SalesIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The cash sale request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_cash_sale_request" });
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The sale query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_sale_query" });
}
