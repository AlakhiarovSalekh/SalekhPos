using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.Subscriptions.Application.Subscriptions;
using SalekhPos.Subscriptions.Contracts.Subscriptions;

namespace SalekhPos.Subscriptions.Api.Subscriptions;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/subscriptions")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("/plans", ListPlans);
        group.MapPost("/", Create);
        group.MapGet("/{subscriptionId:guid}", Get);
        group.MapPut("/{subscriptionId:guid}/plan", ChangePlan);
        group.MapPost("/{subscriptionId:guid}/cancellation", Cancel);
        group.MapPost("/{subscriptionId:guid}/renewals", Renew);
        group.MapGet("/{subscriptionId:guid}/entitlements", GetEntitlements);
    }
    private static Task<IReadOnlyList<PlanResponse>> ListPlans(Guid organizationId, HttpContext context,
        ISubscriptionService service, CancellationToken token) => service.ListPlansAsync(Identity(context), organizationId, token);
    private static Task<SubscriptionResponse> Create(Guid organizationId, CreateSubscriptionRequest request, HttpContext context,
        ISubscriptionService service, CancellationToken token) => service.CreateAsync(Identity(context), organizationId, request, token);
    private static Task<SubscriptionResponse> Get(Guid organizationId, Guid subscriptionId, HttpContext context,
        ISubscriptionService service, CancellationToken token) => service.GetAsync(Identity(context), organizationId, subscriptionId, token);
    private static Task<SubscriptionResponse> ChangePlan(Guid organizationId, Guid subscriptionId, ChangePlanRequest request,
        HttpContext context, ISubscriptionService service, CancellationToken token) =>
        service.ChangePlanAsync(Identity(context), organizationId, subscriptionId, request, token);
    private static Task<SubscriptionResponse> Cancel(Guid organizationId, Guid subscriptionId, CancelSubscriptionRequest request,
        HttpContext context, ISubscriptionService service, CancellationToken token) =>
        service.CancelAsync(Identity(context), organizationId, subscriptionId, request, token);
    private static Task<SubscriptionResponse> Renew(Guid organizationId, Guid subscriptionId, RenewSubscriptionRequest request,
        HttpContext context, ISubscriptionService service, CancellationToken token) =>
        service.RenewAsync(Identity(context), organizationId, subscriptionId, request, token);
    private static Task<EntitlementResponse> GetEntitlements(Guid organizationId, Guid subscriptionId, HttpContext context,
        ISubscriptionService service, CancellationToken token) =>
        service.GetEntitlementsAsync(Identity(context), organizationId, subscriptionId, token);
    private static SubscriptionIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
}
