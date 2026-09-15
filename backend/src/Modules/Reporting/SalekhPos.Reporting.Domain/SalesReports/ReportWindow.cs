namespace SalekhPos.Reporting.Domain.SalesReports;

public sealed record ReportWindow
{
    public DateTimeOffset From { get; }
    public DateTimeOffset To { get; }

    public ReportWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero || from >= to)
            throw new ArgumentException("Report window is invalid.");
        if (to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Report window is too large.");
        From = from;
        To = to;
    }
}
