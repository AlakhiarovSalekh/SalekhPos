using SalekhPos.Subscriptions.Domain.Entitlements;
using SalekhPos.Subscriptions.Domain.Plans;
using SalekhPos.Subscriptions.Domain.Subscriptions;

namespace SalekhPos.Tests;

public sealed class SubscriptionDomainInvariantTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-17T00:00:00Z");
    [Fact]
    public void Scheduled_cancellation_becomes_final_at_renewal_boundary()
    {
        var subscription = NewSubscription(); subscription.ScheduleCancellation();
        subscription.Renew(subscription.PeriodEnd, subscription.PeriodEnd.AddMonths(1));
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.Equal(Start.AddMonths(1), subscription.CanceledAt);
    }

    [Fact]
    public void Renewal_requires_exact_contiguous_period()
    {
        var subscription = NewSubscription();
        Assert.Throws<InvalidOperationException>(() => subscription.Renew(Start.AddDays(20), Start.AddMonths(2)));
        subscription.Renew(Start.AddMonths(1), Start.AddMonths(2));
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void Entitlement_supports_finite_and_unlimited_limits()
    {
        var snapshot = new EntitlementSnapshot(Guid.NewGuid(), new Dictionary<string, long>
        { ["registers"] = 2, ["sales"] = -1 }, Start.AddMonths(1));
        Assert.True(snapshot.Allows("registers", 1, Start));
        Assert.False(snapshot.Allows("registers", 2, Start));
        Assert.True(snapshot.Allows("sales", 1_000_000, Start));
        Assert.False(snapshot.Allows("sales", 0, Start.AddMonths(1)));
    }

    [Fact]
    public void Plan_normalizes_codes_and_rejects_invalid_limits()
    {
        var plan = new SubscriptionPlan(Guid.NewGuid(), " PRO ", "Professional", 49.99m, "gel",
            BillingInterval.Monthly, new Dictionary<string, long> { ["Registers"] = 5 });
        Assert.Equal("pro", plan.Code); Assert.Equal(5, plan.Entitlements["registers"]);
        Assert.Throws<ArgumentException>(() => new SubscriptionPlan(Guid.NewGuid(), "bad code", "Bad", 1m, "GEL",
            BillingInterval.Monthly, new Dictionary<string, long>()));
    }
    private static Subscription NewSubscription() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddMonths(1));
}
