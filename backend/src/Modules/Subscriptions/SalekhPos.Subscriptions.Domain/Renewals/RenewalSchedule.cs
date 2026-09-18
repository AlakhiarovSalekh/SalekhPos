namespace SalekhPos.Subscriptions.Domain.Renewals;

public static class RenewalSchedule
{
    public static DateTimeOffset Next(DateTimeOffset periodEnd, Plans.BillingInterval interval)
    {
        if (periodEnd == default || periodEnd.Offset != TimeSpan.Zero) throw new ArgumentException("Period end must be UTC.");
        return interval switch
        {
            Plans.BillingInterval.Monthly => periodEnd.AddMonths(1),
            Plans.BillingInterval.Annual => periodEnd.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(interval))
        };
    }
}
