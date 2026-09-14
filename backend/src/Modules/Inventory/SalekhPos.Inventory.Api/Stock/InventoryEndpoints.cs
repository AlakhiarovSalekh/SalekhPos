using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
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

        var web = app.MapGroup("/bff/v1/organizations/{organizationId:guid}/inventory")
            .RequireRateLimiting("business");
        web.MapGet("/access", async (Guid organizationId, HttpContext context, IInventoryLedger ledger, CancellationToken ct) =>
        {
            if (!await AuthenticateWeb(context) || !TryIdentity(context, out var identity) || organizationId == Guid.Empty) return Results.Unauthorized();
            return Results.Ok(await ledger.ReadAccessAsync(identity, organizationId, ct));
        });
        web.MapGet("/branches/{branchId:guid}/stock", async (Guid organizationId, Guid branchId, HttpContext context, IInventoryLedger ledger, CancellationToken ct) =>
        {
            if (!await AuthenticateWeb(context) || !TryIdentity(context, out var identity)) return Results.Unauthorized();
            if (!Query(context, out var size, out var after)) return Invalid();
            var page = await ledger.ReadStockAsync(identity, organizationId, branchId, size, after, ct);
            return Results.Ok(new
            {
                items = page.Items.Select(item => new { item.ProductId, item.Sku, item.Name, Quantity = item.Quantity.ToString(CultureInfo.InvariantCulture) }),
                page.NextCursor
            });
        });
        web.MapPost("/branches/{branchId:guid}/movements", async (Guid organizationId, Guid branchId,
            CreateWebStockMovementRequest request, HttpContext context, IInventoryLedger ledger, IAntiforgery antiforgery,
            IConfiguration configuration, CancellationToken ct) =>
        {
            if (!await AuthenticateWeb(context) || !TryIdentity(context, out var identity)) return Results.Unauthorized();
            if (!await ValidCsrf(context, antiforgery, configuration)) return Invalid();
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operation) || operation == Guid.Empty) return Invalid();
            if (!TryKind(request.Kind, out var kind) || kind is StockMovementKind.Sale or StockMovementKind.Return) return Invalid();
            if (!decimal.TryParse(request.Quantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity)) return Invalid();
            try
            {
                var result = await ledger.RecordAsync(identity, new(organizationId, branchId, request.ProductId,
                    Guid.NewGuid(), operation, kind, quantity, request.Reason, request.OccurredAt), ct);
                var response = WebMovement(result.Movement);
                return result.Created ? Results.Created($"/bff/v1/organizations/{organizationId:D}/inventory/branches/{branchId:D}/movements/{result.Movement.Id:D}", response) : Results.Ok(response);
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }
    private static InventoryIdentity Identity(HttpContext c) => new(c.User.FindFirst("iss")!.Value, c.User.FindFirst("sub")!.Value);
    private static object WebMovement(StockMovementResponse movement) => new
    {
        movement.Id,
        movement.BranchId,
        movement.ProductId,
        movement.Kind,
        Quantity = movement.Quantity.ToString(CultureInfo.InvariantCulture),
        movement.Reason,
        movement.OccurredAt,
        movement.RecordedAt
    };
    private static async Task<bool> AuthenticateWeb(HttpContext context)
    {
        try
        {
            var result = await context.AuthenticateAsync("WebSession");
            if (!result.Succeeded || result.Principal is null) return false;
            context.User = result.Principal;
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }
    private static bool TryIdentity(HttpContext context, out InventoryIdentity identity)
    {
        var issuer = context.User.FindFirst("iss")?.Value;
        var subject = context.User.FindFirst("sub")?.Value;
        if (issuer is null || subject is null) { identity = null!; return false; }
        try { identity = new(issuer, subject); return true; }
        catch (ArgumentException) { identity = null!; return false; }
    }

    private static async Task<bool> ValidCsrf(HttpContext context, IAntiforgery antiforgery, IConfiguration configuration)
    {
        if (context.Request.Headers.Origin.Count != 1) return false;
        var expected = configuration["WebAuthentication:PublicOrigin"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(expected)) return false;
        if (!string.Equals(context.Request.Headers.Origin, expected, StringComparison.Ordinal)) return false;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
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
