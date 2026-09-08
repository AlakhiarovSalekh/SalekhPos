using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Application.Receipts;

namespace SalekhPos.Sales.Api.Receipts;

public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}/receipt",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context, ISaleReader reader,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var identity = new SalesIdentity(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value);
                    var sale = await reader.ReadAsync(identity, organizationId, branchId, saleId, cancellationToken);
                    return sale is null ? Results.NotFound() : Results.Ok(ReceiptProjection.From(sale));
                }
                catch (ArgumentException)
                {
                    return Results.Problem(statusCode: 400, title: "The receipt query is invalid",
                        extensions: new Dictionary<string, object?> { ["code"] = "invalid_receipt_query" });
                }
            }).RequireAuthorization().RequireRateLimiting("business");
    }
}
