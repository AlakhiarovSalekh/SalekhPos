using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.FeatureManagement.Application.Features;
using SalekhPos.FeatureManagement.Contracts.Features;

namespace SalekhPos.FeatureManagement.Api.Features;

public static class FeatureEndpoints
{
    public static void MapFeatureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/features")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("/{key}", Evaluate);
        group.MapPut("/{key}/override", Override);
    }

    private static Task<FeatureDecisionResponse> Evaluate(Guid organizationId, string key,
        HttpContext context, IFeaturePolicyService service, CancellationToken cancellationToken) =>
        service.EvaluateAsync(Identity(context), organizationId, key, cancellationToken);

    private static async Task<IResult> Override(Guid organizationId, string key,
        FeatureOverrideRequest request, HttpContext context, IFeaturePolicyService service,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
            return Results.Problem(statusCode: 400, title: "The feature override is invalid",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_feature_override" });
        return Results.Ok(await service.SetOverrideAsync(Identity(context), organizationId, key,
            request.Enabled, request.Reason, cancellationToken));
    }

    private static FeatureIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
}
