using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Payments.Application.Payments;

namespace SalekhPos.Payments.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/{saleId:guid}/payment",
            async (Guid organizationId, Guid branchId, Guid saleId, HttpContext context, IPaymentReader reader,
                CancellationToken cancellationToken) =>
            {
                var identity = new PaymentIdentity(context.User.FindFirst("iss")!.Value,
                    context.User.FindFirst("sub")!.Value);
                var payment = await reader.ReadForSaleAsync(identity, organizationId, branchId, saleId,
                    cancellationToken);
                return payment is null ? Results.NotFound() : Results.Ok(payment);
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/returns/{returnId:guid}/refund",
            async (Guid organizationId, Guid branchId, Guid returnId, HttpContext context, IPaymentReader reader,
                CancellationToken cancellationToken) =>
            {
                var refund = await reader.ReadForReturnAsync(Identity(context), organizationId, branchId, returnId,
                    cancellationToken);
                return refund is null ? Results.NotFound() : Results.Ok(refund);
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/voids/{voidId:guid}/refund",
            async (Guid organizationId, Guid branchId, Guid voidId, HttpContext context, IPaymentReader reader,
                CancellationToken cancellationToken) =>
            {
                var refund = await reader.ReadForVoidAsync(Identity(context), organizationId, branchId, voidId,
                    cancellationToken);
                return refund is null ? Results.NotFound() : Results.Ok(refund);
            }).RequireAuthorization().RequireRateLimiting("business");
    }

    private static PaymentIdentity Identity(HttpContext context) => new(context.User.FindFirst("iss")!.Value,
        context.User.FindFirst("sub")!.Value);
}
