using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using SalekhPos.Customers.Application.Customers;
using SalekhPos.Customers.Contracts.Customers;

namespace SalekhPos.Customers.Api.Customers;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/customers")
            .RequireAuthorization("business-api").RequireRateLimiting("business");

        group.MapGet("", async (Guid organizationId, HttpContext context, ICustomerDirectory customers,
            CancellationToken cancellationToken) =>
        {
            if (!TryPage(context, out var pageSize, out var after)) return Invalid();
            return Results.Ok(await customers.ListAsync(Identity(context), organizationId, pageSize, after,
                cancellationToken));
        });

        group.MapGet("/{customerId:guid}", async (Guid organizationId, Guid customerId, HttpContext context,
            ICustomerDirectory customers, CancellationToken cancellationToken) =>
        {
            var result = await customers.ReadAsync(Identity(context), organizationId, customerId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("", async (Guid organizationId, CreateCustomerRequest request, HttpContext context,
            ICustomerDirectory customers, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty || organizationId == Guid.Empty) return Invalid();
            try
            {
                var result = await customers.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(),
                    operationId, request.Code, request.DisplayName, request.Email, request.Phone), cancellationToken);
                var location = $"/api/v1/organizations/{organizationId:D}/customers/{result.Customer.Id:D}";
                return result.Created ? Results.Created(location, result.Customer) : Results.Ok(result.Customer);
            }
            catch (ArgumentException) { return Invalid(); }
        });

        group.MapPut("/{customerId:guid}", async (Guid organizationId, Guid customerId,
            UpdateCustomerRequest request, HttpContext context, ICustomerDirectory customers,
            IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            if (!await ValidateMutation(context, antiforgery)) return InvalidBrowserMutation();
            try
            {
                return Results.Ok(await customers.UpdateAsync(Identity(context), new(organizationId, customerId,
                    request.DisplayName, request.Email, request.Phone, request.IsActive, request.ExpectedVersion),
                    cancellationToken));
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static CustomerIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);

    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The customer request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_customer_request" });

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
