using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Promotions.Application.Campaigns;
using SalekhPos.Promotions.Contracts.Campaigns;

namespace SalekhPos.Promotions.Api.Campaigns;

public static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/branches/{branchId:guid}/promotions")
            .RequireAuthorization("business-api").RequireRateLimiting("business");

        group.MapGet("", async (Guid organizationId, Guid branchId, HttpContext context,
            IPromotionService service, CancellationToken ct) =>
        {
            if (!TryPage(context, out var size, out var after)) return Invalid();
            return Results.Ok(await service.ListAsync(Identity(context), organizationId, branchId, size, after, ct));
        });
        group.MapGet("/{promotionId:guid}", async (Guid organizationId, Guid branchId, Guid promotionId,
            HttpContext context, IPromotionService service, CancellationToken ct) =>
        {
            var value = await service.ReadAsync(Identity(context), organizationId, branchId, promotionId, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        });

        group.MapPost("", async (Guid organizationId, Guid branchId, CreatePromotionRequest request,
            HttpContext context, IPromotionService service, IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!await ValidateMutation(context, antiforgery)
                || !Guid.TryParseExact(context.Request.Headers["Idempotency-Key"], "D", out var operationId)
                || operationId == Guid.Empty) return Invalid();
            try
            {
                var branchScope = request.BranchId ?? branchId;
                if (request.BranchId.HasValue && request.BranchId.Value != branchId) return Invalid();
                var result = await service.CreateAsync(Identity(context), new(organizationId, Guid.NewGuid(), operationId,
                    request.Code, request.Name, branchScope, request.DiscountKind, request.Value, request.Currency,
                    request.MinimumSubtotal, request.StartsAt, request.EndsAt), ct);
                var location = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions/{result.Promotion.Id:D}";
                return result.Created ? Results.Created(location, result.Promotion) : Results.Ok(result.Promotion);
            }
            catch (ArgumentException) { return Invalid(); }
        });
        group.MapPost("/evaluate", async (Guid organizationId, Guid branchId, EvaluatePromotionRequest request,
            HttpContext context, IPromotionService service, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.EvaluateAsync(Identity(context), organizationId, branchId,
                    request.Subtotal, request.Currency, request.At, ct));
            }
            catch (ArgumentException) { return Invalid(); }
        });

        group.MapPost("/{promotionId:guid}/deactivate", async (Guid organizationId, Guid branchId,
            Guid promotionId, ChangePromotionStatusRequest request, HttpContext context,
            IPromotionService service, IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!await ValidateMutation(context, antiforgery) || request.ExpectedVersion < 1) return Invalid();
            try
            {
                return Results.Ok(await service.DeactivateAsync(Identity(context), organizationId, branchId,
                    promotionId, request.ExpectedVersion, ct));
            }
            catch (ArgumentException) { return Invalid(); }
        });
    }

    private static PromotionIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The promotion request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_promotion_request" });

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
            if (cursors.Count != 1 || !Guid.TryParseExact(cursors[0], "D", out var cursor) || cursor == Guid.Empty)
                return false;
            after = cursor;
        }
        return true;
    }
}
