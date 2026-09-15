using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using SalekhPos.Purchasing.Application.PurchaseOrders;
using SalekhPos.Purchasing.Contracts.PurchaseOrders;

namespace SalekhPos.Purchasing.Api.PurchaseOrders;

public static class PurchaseOrderEndpoints
{
    public static void MapPurchaseOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/purchase-orders")
            .RequireAuthorization("business-api").RequireRateLimiting("business");

        group.MapGet("", async (Guid organizationId, Guid branchId, HttpContext context,
            IPurchaseOrderService orders, CancellationToken cancellationToken) =>
        {
            if (!TryPage(context, out var pageSize, out var after)) return Invalid();
            return Results.Ok(await orders.ListAsync(Identity(context), organizationId, branchId,
                pageSize, after, cancellationToken));
        });

        group.MapGet("/{orderId:guid}", async (Guid organizationId, Guid branchId, Guid orderId,
            HttpContext context, IPurchaseOrderService orders, CancellationToken cancellationToken) =>
        {
            var result = await orders.ReadAsync(Identity(context), organizationId, branchId,
                orderId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("", async (Guid organizationId, Guid branchId, CreatePurchaseOrderRequest request,
            HttpContext context, IPurchaseOrderService orders, IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty || organizationId == Guid.Empty || branchId == Guid.Empty) return Invalid();
            try
            {
                var lines = request.Lines.Select(line => new CreatePurchaseOrderLine(
                    line.ProductId, line.Quantity, line.UnitCost)).ToArray();
                var result = await orders.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(),
                    operationId, branchId, request.SupplierId, request.Currency, request.Reference, lines),
                    cancellationToken);
                var location = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/purchase-orders/{result.Order.Id:D}";
                return result.Created ? Results.Created(location, result.Order) : Results.Ok(result.Order);
            }
            catch (ArgumentException) { return Invalid(); }
        });

        MapStatus(group, "submit", "submitted");
        MapStatus(group, "approve", "approved");
        MapStatus(group, "cancel", "cancelled");
    }

    private static void MapStatus(RouteGroupBuilder group, string action, string targetStatus)
    {
        group.MapPost($"/{{orderId:guid}}/{action}", async (Guid organizationId, Guid branchId, Guid orderId,
            ChangePurchaseOrderStatusRequest request, HttpContext context, IPurchaseOrderService orders,
            IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)) return InvalidBrowserMutation();
            try
            {
                return Results.Ok(await orders.ChangeStatusAsync(Identity(context), new(organizationId,
                    branchId, orderId, targetStatus, request.ExpectedVersion), cancellationToken));
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static PurchasingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The purchase order request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_purchase_order_request" });

    private static IResult InvalidBrowserMutation() => Results.Problem(statusCode: 400,
        title: "The browser request could not be verified",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_browser_request" });

    private static async Task<bool> ValidateMutation(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Headers.ContainsKey("Authorization")) return true;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static bool TryPage(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out pageSize) || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }
}
