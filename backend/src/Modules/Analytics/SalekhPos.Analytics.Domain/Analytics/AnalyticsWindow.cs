namespace SalekhPos.Analytics.Domain.Analytics;

public sealed record AnalyticsWindow
{
    public AnalyticsWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (from == default || to == default || from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero)
            throw new ArgumentException("Analytics window must use UTC timestamps.");
        if (to <= from || to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Analytics window is invalid.");
        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }
    public DateTimeOffset To { get; }
}
