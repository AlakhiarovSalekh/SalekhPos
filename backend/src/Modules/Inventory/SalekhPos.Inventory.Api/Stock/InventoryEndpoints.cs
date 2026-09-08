using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Inventory.Application.Stock;
using SalekhPos.Inventory.Contracts.Stock;
using SalekhPos.Inventory.Domain.StockMovements;

namespace SalekhPos.Inventory.Api.Stock;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/inventory").RequireAuthorization().RequireRateLimiting("business");
        group.MapGet("/stock", async (Guid organizationId, Guid branchId, HttpContext context, IInventoryLedger ledger, CancellationToken ct) =>
        {
            if (!Query(context, out var size, out var after)) return Invalid();
            return Results.Ok(await ledger.ReadStockAsync(Identity(context), organizationId, branchId, size, after, ct));
        });
        group.MapPost("/movements", async (Guid organizationId, Guid branchId, CreateStockMovementRequest request, HttpContext context, IInventoryLedger ledger, CancellationToken ct) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operation) || operation == Guid.Empty
              || !TryKind(request.Kind, out var kind)) return Invalid();
            try
            {
                var result = await ledger.RecordAsync(Identity(context), new(organizationId, branchId, request.ProductId, Guid.NewGuid(), operation, kind, request.Quantity, request.Reason, request.OccurredAt), ct);
                return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/inventory/movements/{result.Movement.Id:D}", result.Movement) : Results.Ok(result.Movement);
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }
    private static InventoryIdentity Identity(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value);
    private static bool TryKind(string? value, out StockMovementKind kind)
    {
        kind = value switch
        {
            "receipt" => StockMovementKind.Receipt,
            "adjustment_in" => StockMovementKind.AdjustmentIn,
            "adjustment_out" => StockMovementKind.AdjustmentOut,
            "sale" => StockMovementKind.Sale,
            "return" => StockMovementKind.Return,
            _ => default
        };
        return value is "receipt" or "adjustment_in" or "adjustment_out" or "sale" or "return";
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The inventory request is invalid", extensions: new Dictionary<string, object?> { { "code", "invalid_inventory_request" } });
    private static bool Query(HttpContext c, out int size, out Guid? after)
    {
        size = 50; after = null; if (c.Request.Query.Keys.Any(k => k is not "pageSize" and not "after")) return false;
        if (c.Request.Query.TryGetValue("pageSize", out var s) && (s.Count != 1 || !int.TryParse(s[0], NumberStyles.None, CultureInfo.InvariantCulture, out size) || size is < 1 or > 100)) return false;
        if (c.Request.Query.TryGetValue("after", out var a)) { if (a.Count != 1 || !Guid.TryParseExact(a[0], "D", out var id) || id == Guid.Empty) return false; after = id; }
        return true;
    }
}
