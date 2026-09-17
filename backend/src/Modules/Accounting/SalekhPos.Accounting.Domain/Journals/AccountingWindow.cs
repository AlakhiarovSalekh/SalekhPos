namespace SalekhPos.Accounting.Domain.Journals;

public sealed record AccountingWindow
{
    public AccountingWindow(DateTimeOffset from, DateTimeOffset to)
    {
        if (from == default || to == default || from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero)
            throw new ArgumentException("Accounting window must use UTC timestamps.");
        if (to <= from || to - from > TimeSpan.FromDays(366))
            throw new ArgumentException("Accounting window is invalid.");
        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }
    public DateTimeOffset To { get; }
}
