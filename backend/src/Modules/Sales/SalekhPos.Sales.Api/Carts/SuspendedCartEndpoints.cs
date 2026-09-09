using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Sales.Application.Carts;
using SalekhPos.Sales.Contracts.Carts;

namespace SalekhPos.Sales.Api.Carts;

public static class SuspendedCartEndpoints
{
    public static void MapSuspendedCartEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/suspended",
            async (Guid organizationId, Guid branchId, SuspendCartRequest request, HttpContext context,
                ISuspendedCartService service, CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                    || operationId == Guid.Empty || request.Lines is null) return Invalid();
                try
                {
                    var result = await service.SuspendAsync(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value, new(organizationId, branchId, Guid.NewGuid(),
                        operationId, request.Lines, request.Note), cancellationToken);
                    return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/sales/suspended/{result.Cart.Id:D}", result.Cart)
                        : Results.Ok(result.Cart);
                }
                catch (ArgumentException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapPost("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/suspended/{cartId:guid}/resume",
            async (Guid organizationId, Guid branchId, Guid cartId, HttpContext context,
                ISuspendedCartService service, CancellationToken cancellationToken) =>
            {
                try
                {
                    var cart = await service.ResumeAsync(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value, organizationId, branchId, cartId, cancellationToken);
                    return cart is null ? Results.NotFound() : Results.Ok(cart);
                }
                catch (ArgumentException) { return Invalid(); }
            }).RequireAuthorization().RequireRateLimiting("business");
    }

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The suspended cart request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_suspended_cart_request" });
}
