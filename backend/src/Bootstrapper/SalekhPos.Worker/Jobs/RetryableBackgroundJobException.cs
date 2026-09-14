namespace SalekhPos.Worker.Jobs;

/// <summary>
/// Explicitly opts a failed attempt into bounded retry. Jobs must only throw this exception
/// when repeating the operation is safe.
/// </summary>
public sealed class RetryableBackgroundJobException : Exception
{
    public RetryableBackgroundJobException()
        : base("The background job reported an explicitly retryable failure.")
    {
    }

    public RetryableBackgroundJobException(Exception innerException)
        : base("The background job reported an explicitly retryable failure.", innerException)
    {
    }
}
