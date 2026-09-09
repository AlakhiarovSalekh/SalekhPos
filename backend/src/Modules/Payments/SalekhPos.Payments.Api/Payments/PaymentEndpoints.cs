using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Payments.Application.Payments;

namespace SalekhPos.Payments.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/payment-events",
            async (Guid organizationId, Guid branchId, HttpContext context, IPaymentReader reader,
                CancellationToken cancellationToken) =>
            {
                if (!TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                try
                {
                    return Results.Ok(await reader.ListEventsAsync(Identity(context), organizationId, branchId,
                    pageSize, after, cancellationToken));
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

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
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The payment event query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_payment_event_query" });
    private static bool TryListQuery(HttpContext context, out int pageSize, out string? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize)
                || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        { if (cursors.Count != 1 || string.IsNullOrWhiteSpace(cursors[0])) return false; after = cursors[0]; }
        return true;
    }
}
