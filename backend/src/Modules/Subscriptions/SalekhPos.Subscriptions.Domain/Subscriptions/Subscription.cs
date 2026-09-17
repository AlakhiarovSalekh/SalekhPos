using SalekhPos.Subscriptions.Domain.Plans;

namespace SalekhPos.Subscriptions.Domain.Subscriptions;

public enum SubscriptionStatus { Trialing, Active, PastDue, Canceled, Expired }

public sealed class Subscription
{
    public Subscription(Guid id, Guid organizationId, Guid planId, DateTimeOffset periodStart,
        DateTimeOffset periodEnd, bool trial = false)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || planId == Guid.Empty) throw new ArgumentException("Subscription identifiers are required.");
        ValidatePeriod(periodStart, periodEnd);
        Id = id; OrganizationId = organizationId; PlanId = planId; PeriodStart = periodStart;
        PeriodEnd = periodEnd; Status = trial ? SubscriptionStatus.Trialing : SubscriptionStatus.Active;
    }
    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }

    public void ChangePlan(Guid planId)
    {
        if (planId == Guid.Empty) throw new ArgumentException("Plan identifier is required.");
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired) throw new InvalidOperationException("Subscription is final.");
        PlanId = planId;
    }
    public void MarkPastDue()
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing)) throw new InvalidOperationException("Subscription cannot become past due.");
        Status = SubscriptionStatus.PastDue;
    }
    public void ScheduleCancellation() {
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired) throw new InvalidOperationException("Subscription is final.");
        CancelAtPeriodEnd = true;
    }
    public void CancelImmediately(DateTimeOffset at)
    {
        ValidateInstant(at);
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired) throw new InvalidOperationException("Subscription is final.");
        Status = SubscriptionStatus.Canceled; CanceledAt = at; CancelAtPeriodEnd = false;
    }
    public void Renew(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        ValidatePeriod(periodStart, periodEnd);
        if (periodStart != PeriodEnd || Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
            throw new InvalidOperationException("Subscription cannot be renewed.");
        if (CancelAtPeriodEnd) { Status = SubscriptionStatus.Canceled; CanceledAt = PeriodEnd; CancelAtPeriodEnd = false; return; }
        PeriodStart = periodStart; PeriodEnd = periodEnd; Status = SubscriptionStatus.Active;
    }
    public void Expire(DateTimeOffset at)
    {
        ValidateInstant(at);
        if (at < PeriodEnd || Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired) throw new InvalidOperationException("Subscription cannot expire.");
        Status = SubscriptionStatus.Expired;
    }
    private static void ValidatePeriod(DateTimeOffset start, DateTimeOffset end)
    {
        ValidateInstant(start); ValidateInstant(end);
        if (end <= start || end - start > TimeSpan.FromDays(370)) throw new ArgumentException("Subscription period is invalid.");
    }
    private static void ValidateInstant(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.");
    }
}
