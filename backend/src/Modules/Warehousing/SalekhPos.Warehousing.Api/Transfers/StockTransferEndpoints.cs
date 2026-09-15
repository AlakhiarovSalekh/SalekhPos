using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Warehousing.Application.Transfers;
using SalekhPos.Warehousing.Contracts.Transfers;

namespace SalekhPos.Warehousing.Api.Transfers;

public static class StockTransferEndpoints
{
    public static void MapStockTransferEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/stock-transfers")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("", async (Guid organizationId, Guid branchId, HttpContext context,
            IStockTransferService transfers, CancellationToken ct) =>
        {
            if (!TryPage(context, out var size, out var after)) return Invalid();
            return Results.Ok(await transfers.ListAsync(Identity(context), organizationId, branchId, size, after, ct));
        });
        group.MapGet("/{transferId:guid}", async (Guid organizationId, Guid branchId, Guid transferId,
            HttpContext context, IStockTransferService transfers, CancellationToken ct) =>
        {
            var value = await transfers.ReadAsync(Identity(context), organizationId, branchId, transferId, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        });
        group.MapPost("", async (Guid organizationId, Guid branchId, CreateStockTransferRequest request,
            HttpContext context, IStockTransferService transfers, IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty) return Invalid();
            try
            {
                var result = await transfers.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(),
                    operationId, branchId, request.DestinationBranchId, request.Reference, request.Lines), ct);
                var location = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/stock-transfers/{result.Transfer.Id:D}";
                return result.Created ? Results.Created(location, result.Transfer) : Results.Ok(result.Transfer);
            }
            catch (ArgumentException) { return Invalid(); }
        });
        MapTransition(group, "dispatch");
        MapTransition(group, "receive");
        MapTransition(group, "cancel");
    }

    private static void MapTransition(RouteGroupBuilder group, string action)
    {
        group.MapPost($"/{{transferId:guid}}/{action}", async (Guid organizationId, Guid branchId, Guid transferId,
            ChangeStockTransferStatusRequest request, HttpContext context, IStockTransferService transfers,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {            if (!await ValidateMutation(context, antiforgery) || request.ExpectedVersion < 1)
                return Invalid();
            try
            {
                var identity = Identity(context);
                var result = action switch
                {
                    "dispatch" => await transfers.DispatchAsync(identity, organizationId, branchId,
                        transferId, request.ExpectedVersion, ct),
                    "receive" => await transfers.ReceiveAsync(identity, organizationId, branchId,
                        transferId, request.ExpectedVersion, ct),
                    "cancel" => await transfers.CancelAsync(identity, organizationId, branchId,
                        transferId, request.ExpectedVersion, ct),
                    _ => throw new ArgumentException("Unsupported stock transfer action.")
                };
                return Results.Ok(result);
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static WarehousingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The stock transfer request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_stock_transfer_request" });

    private static async Task<bool> ValidateMutation(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Request.Headers.ContainsKey("Authorization")) return true;
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool TryPage(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50;
        after = null;
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
