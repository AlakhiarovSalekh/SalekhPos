namespace SalekhPos.Subscriptions.Contracts.Subscriptions;

public sealed record PlanResponse(Guid PlanId, string Code, string Name, decimal Price, string Currency,
    string Interval, IReadOnlyDictionary<string, long> Entitlements, bool Active);
public sealed record CreateSubscriptionRequest(Guid OperationId, Guid SubscriptionId, Guid PlanId,
    DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, bool Trial);
public sealed record SubscriptionResponse(Guid SubscriptionId, Guid PlanId, string Status,
    DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, bool CancelAtPeriodEnd, DateTimeOffset? CanceledAt);
public sealed record ChangePlanRequest(Guid OperationId, Guid PlanId);
public sealed record CancelSubscriptionRequest(Guid OperationId, bool Immediately, DateTimeOffset? EffectiveAt);
public sealed record RenewSubscriptionRequest(Guid OperationId, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
public sealed record EntitlementResponse(Guid SubscriptionId, IReadOnlyDictionary<string, long> Limits, DateTimeOffset ValidUntil);
