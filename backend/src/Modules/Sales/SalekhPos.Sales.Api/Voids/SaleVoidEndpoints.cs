using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Sales.Application.Voids;
using SalekhPos.Sales.Contracts.Voids;

namespace SalekhPos.Sales.Api.Voids;

public static class SaleVoidEndpoints
{
    public static void MapSaleVoidEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/voids",
            async (Guid organizationId, Guid branchId, VoidSaleRequest request, HttpContext context,
                ISaleVoidService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty) return Invalid();
                try
                {
                    var result = await service.VoidAsync(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value, new(organizationId, branchId, Guid.NewGuid(),
                        operationId, request.SaleId, request.Reason), cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/sales/voids/{result.Void.Id:D}", result.Void)
                        : Results.Ok(result.Void);
                }
                catch (ArgumentException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The sale void request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_sale_void_request" });
}
