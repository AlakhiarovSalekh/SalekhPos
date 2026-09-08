using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Catalog.Application.Products;
using SalekhPos.Catalog.Contracts.Products;

namespace SalekhPos.Catalog.Api.Products;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/products")
            .RequireAuthorization().RequireRateLimiting("business");
        group.MapGet("", async (Guid organizationId, HttpContext context, IProductCatalog catalog, CancellationToken cancellationToken) =>
        {
            if (!TryQuery(context, out var pageSize, out var after)) return InvalidQuery();
            return Results.Ok(await catalog.ReadAsync(Identity(context), organizationId, pageSize, after, cancellationToken));
        });
        group.MapGet("/{productId:guid}", async (Guid organizationId, Guid productId, HttpContext context,
            IProductCatalog catalog, CancellationToken cancellationToken) =>
        {
            var product = await catalog.ReadOneAsync(Identity(context), organizationId, productId, null, cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });
        group.MapGet("/by-barcode/{barcode}", async (Guid organizationId, string barcode, HttpContext context,
            IProductCatalog catalog, CancellationToken cancellationToken) =>
        {
            try
            {
                var product = await catalog.ReadOneAsync(Identity(context), organizationId, null, barcode, cancellationToken);
                return product is null ? Results.NotFound() : Results.Ok(product);
            }
            catch (ArgumentException) { return InvalidQuery(); }
        });
        group.MapPost("", async (Guid organizationId, CreateProductRequest request, HttpContext context,
            IProductCatalog catalog, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty || organizationId == Guid.Empty) return InvalidQuery();
            ProductWriteResult result;
            try
            {
                result = await catalog.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(), operationId,
                    request.Sku, request.Name, request.UnitCode, request.Barcode), cancellationToken);
            }
            catch (ArgumentException) { return InvalidQuery(); }
            return result.Created
                ? Results.Created($"/api/v1/organizations/{organizationId:D}/products/{result.Product.Id:D}", result.Product)
                : Results.Ok(result.Product);
        });
        group.MapPut("/{productId:guid}", async (Guid organizationId, Guid productId, UpdateProductRequest request,
            HttpContext context, IProductCatalog catalog, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await catalog.UpdateAsync(Identity(context), new(organizationId, productId,
                    request.Name, request.UnitCode, request.Barcode, request.IsActive, request.ExpectedVersion), cancellationToken));
            }
            catch (ArgumentException) { return InvalidQuery(); }
        });
    }

    private static CatalogIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult InvalidQuery() => Results.Problem(statusCode: 400, title: "The product request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_product_request" });

    private static bool TryQuery(HttpContext context, out int pageSize, out Guid? after)
    {
        pageSize = 50; after = null;
        if (context.Request.Query.Keys.Any(key => key is not "pageSize" and not "after")) return false;
        if (context.Request.Query.TryGetValue("pageSize", out var sizes)
            && (sizes.Count != 1 || !int.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out pageSize)
                || pageSize is < 1 or > 100)) return false;
        if (context.Request.Query.TryGetValue("after", out var cursors))
        {
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor) || cursor == Guid.Empty) return false;
            after = cursor;
        }
        return true;
    }
}
