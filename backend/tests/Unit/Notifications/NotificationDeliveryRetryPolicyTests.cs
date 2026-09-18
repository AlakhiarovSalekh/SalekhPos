using SalekhPos.Notifications.Domain.Notifications;

namespace SalekhPos.Tests.Notifications;

public sealed class NotificationDeliveryRetryPolicyTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(204)]
    public void Success_statuses_complete_without_retry(int statusCode)
    {
        var decision = NotificationDeliveryRetryPolicy.Classify(
            1,
            statusCode,
            false,
            DateTimeOffset.Parse("2026-09-18T20:00:00Z"));

        Assert.True(decision.Succeeded);
        Assert.False(decision.Retry);
        Assert.Null(decision.ErrorCode);
        Assert.Null(decision.RetryAt);
    }

    [Fact]
    public void Retry_after_is_bounded_and_tenth_attempt_is_terminal()
    {
        var now = DateTimeOffset.Parse("2026-09-18T20:00:00Z");
        var retry = NotificationDeliveryRetryPolicy.Classify(
            1,
            429,
            false,
            now,
            TimeSpan.FromDays(2));
        Assert.True(retry.Retry);
        Assert.Equal(now.AddHours(6), retry.RetryAt);

        var terminal = NotificationDeliveryRetryPolicy.Classify(10, 503, false, now);
        Assert.False(terminal.Succeeded);
        Assert.False(terminal.Retry);
        Assert.Equal("http_503", terminal.ErrorCode);
        Assert.Null(terminal.RetryAt);
    }
}
