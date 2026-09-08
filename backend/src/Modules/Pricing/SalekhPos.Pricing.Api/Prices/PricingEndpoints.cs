using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Pricing.Application.Prices;
using SalekhPos.Pricing.Contracts.Prices;
using SalekhPos.Pricing.Domain.Prices;

namespace SalekhPos.Pricing.Api.Prices;

public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/pricing")
            .RequireAuthorization().RequireRateLimiting("business");
        group.MapPost("/prices", async (Guid organizationId, SchedulePriceRequest request,
            HttpContext context, IPriceBook prices, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty || !TryMode(request.TaxMode, out var mode)) return Invalid();
            try
            {
                var result = await prices.ScheduleAsync(Identity(context), new SchedulePriceCommand(organizationId,
                    Guid.NewGuid(), operationId, request.ProductId, request.BranchId, request.Amount, request.Currency,
                    mode, request.TaxRate, request.ValidFrom, request.ValidUntil), cancellationToken);
                return result.Created ? Results.Created($"/api/v1/organizations/{organizationId:D}/pricing/prices/{result.Price.Id:D}", result.Price)
                    : Results.Ok(result.Price);
            }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapGet("/resolve", async (Guid organizationId, HttpContext context, IPriceBook prices,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParseExact(context.Request.Query["branchId"], "D", out var branchId) || branchId == Guid.Empty
                || !Guid.TryParseExact(context.Request.Query["productId"], "D", out var productId) || productId == Guid.Empty
                || !DateTimeOffset.TryParseExact(context.Request.Query["at"], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var at) || at.Offset != TimeSpan.Zero) return Invalid();
            var result = await prices.ResolveAsync(Identity(context), organizationId, branchId, productId, at, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }

    private static PricingIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static bool TryMode(string? value, out TaxMode mode)
    {
        mode = value == "inclusive" ? TaxMode.Inclusive : TaxMode.Exclusive;
        return value is "inclusive" or "exclusive";
    }
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "The pricing request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_pricing_request" });
}
