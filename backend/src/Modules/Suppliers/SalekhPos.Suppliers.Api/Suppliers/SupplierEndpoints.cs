using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using SalekhPos.Suppliers.Application.Suppliers;
using SalekhPos.Suppliers.Contracts.Suppliers;

namespace SalekhPos.Suppliers.Api.Suppliers;

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/suppliers")
            .RequireAuthorization("business-api").RequireRateLimiting("business");

        group.MapGet("", async (Guid organizationId, HttpContext context, ISupplierDirectory suppliers,
            CancellationToken cancellationToken) =>
        {
            if (!TryPage(context, out var pageSize, out var after)) return Invalid();
            return Results.Ok(await suppliers.ListAsync(Identity(context), organizationId, pageSize, after,
                cancellationToken));
        });

        group.MapGet("/{supplierId:guid}", async (Guid organizationId, Guid supplierId, HttpContext context,
            ISupplierDirectory suppliers, CancellationToken cancellationToken) =>
        {
            var result = await suppliers.ReadAsync(Identity(context), organizationId, supplierId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("", async (Guid organizationId, CreateSupplierRequest request, HttpContext context,
            ISupplierDirectory suppliers, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty || organizationId == Guid.Empty) return Invalid();
            try
            {
                var result = await suppliers.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(),
                    operationId, request.Code, request.Name, request.TaxId, request.Email, request.Phone), cancellationToken);
                var location = $"/api/v1/organizations/{organizationId:D}/suppliers/{result.Supplier.Id:D}";
                return result.Created ? Results.Created(location, result.Supplier) : Results.Ok(result.Supplier);
            }
            catch (ArgumentException) { return Invalid(); }
        });

        group.MapPut("/{supplierId:guid}", async (Guid organizationId, Guid supplierId,
            UpdateSupplierRequest request, HttpContext context, ISupplierDirectory suppliers,
            IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)) return InvalidBrowserMutation();
            try
            {
                return Results.Ok(await suppliers.UpdateAsync(Identity(context), new(organizationId, supplierId,
                    request.Name, request.TaxId, request.Email, request.Phone, request.IsActive, request.ExpectedVersion),
                    cancellationToken));
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static SupplierIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The supplier request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_supplier_request" });

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
