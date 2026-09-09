using System.Globalization;
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
        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/voids",
            async (Guid organizationId, Guid branchId, HttpContext context, ISaleVoidReader reader,
                CancellationToken cancellationToken) =>
            {
                if (!TryListQuery(context, out var pageSize, out var after)) return InvalidQuery();
                try
                {
                    return Results.Ok(await reader.ListAsync(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value, organizationId, branchId, pageSize, after,
                        cancellationToken));
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

        app.MapGet("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/sales/voids/{voidId:guid}",
            async (Guid organizationId, Guid branchId, Guid voidId, HttpContext context, ISaleVoidReader reader,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await reader.ReadAsync(context.User.FindFirst("iss")!.Value,
                        context.User.FindFirst("sub")!.Value, organizationId, branchId, voidId,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException) { return InvalidQuery(); }
            }).RequireAuthorization().RequireRateLimiting("business");

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
    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The sale void query is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_sale_void_query" });
    private static bool TryListQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture,
                out pageSize) || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor)
                || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }
}
