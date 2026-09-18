namespace SalekhPos.Integrations.Domain.Webhooks;

public sealed record WebhookAttemptDecision(bool Succeeded, bool Retry, string? ErrorCode, DateTimeOffset? RetryAt);

public static class WebhookRetryPolicy
{
    private static readonly int[] RetryableStatuses = [408, 425, 429, 500, 502, 503, 504];
    private static readonly int[] BackoffSeconds = [5, 15, 30, 60, 120, 300, 600, 1200, 1800];

    public static WebhookAttemptDecision Classify(int attemptNumber, int? statusCode, bool transportFailure,
        DateTimeOffset now, TimeSpan? retryAfter = null)
    {
        if (attemptNumber is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        if (statusCode is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        if (retryAfter < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retryAfter));

        if (statusCode is >= 200 and <= 299)
        {
            return new(true, false, null, null);
        }

        var retryable = transportFailure || statusCode is null || RetryableStatuses.Contains(statusCode.Value);
        if (!retryable || attemptNumber >= 10)
        {
            return new(false, false, Error(statusCode, transportFailure), null);
        }

        var boundedRetryAfter = retryAfter.HasValue
            ? TimeSpan.FromSeconds(Math.Min(retryAfter.Value.TotalSeconds, 21600))
            : TimeSpan.FromSeconds(BackoffSeconds[Math.Min(attemptNumber - 1, BackoffSeconds.Length - 1)]);
        return new(false, true, Error(statusCode, transportFailure), now + boundedRetryAfter);
    }

    private static string Error(int? statusCode, bool transportFailure) =>
        transportFailure || statusCode is null ? "transport_failure" : "http_" + statusCode.Value;
}
