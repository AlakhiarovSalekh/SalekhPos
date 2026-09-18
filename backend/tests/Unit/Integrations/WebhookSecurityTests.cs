using System.Net;
using System.Text;
using SalekhPos.Integrations.Domain.Security;
using SalekhPos.Integrations.Domain.Webhooks;

namespace SalekhPos.Tests.Integrations;

public sealed class WebhookSecurityTests
{
    [Fact]
    public void Signature_round_trip_uses_versioned_hmac_sha256()
    {
        var secret = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var body = Encoding.UTF8.GetBytes("{\"event\":\"sale.completed\"}");
        var signature = WebhookSignature.Sign(secret, 1_800_000_000, body);

        Assert.StartsWith("v1=", signature, StringComparison.Ordinal);
        Assert.Equal(67, signature.Length);
        Assert.True(WebhookSignature.Verify(secret, 1_800_000_000, body, signature));
        Assert.False(WebhookSignature.Verify(secret, 1_800_000_001, body, signature));
    }

    [Theory]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("http://example.com/hook")]
    [InlineData("https://user:password@example.com/hook")]
    public void Endpoint_policy_rejects_unsafe_uri(string endpoint) =>
        Assert.Throws<ArgumentException>(() => WebhookEndpointPolicy.ValidateUri(endpoint));

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.10.20")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("::1")]
    public void Endpoint_policy_rejects_non_public_resolutions(string address)
    {
        var ip = IPAddress.Parse(address);
        Assert.True(WebhookEndpointPolicy.IsUnsafe(ip));
        Assert.Throws<InvalidOperationException>(() => WebhookEndpointPolicy.ValidateResolvedAddresses([ip]));
    }

    [Fact]
    public void Endpoint_policy_accepts_public_addresses()
    {
        WebhookEndpointPolicy.ValidateResolvedAddresses([
            IPAddress.Parse("8.8.8.8"),
            IPAddress.Parse("2606:4700:4700::1111")
        ]);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(425)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void Retry_policy_retries_transient_statuses(int status)
    {
        var now = DateTimeOffset.Parse("2026-09-18T10:00:00Z");
        var result = WebhookRetryPolicy.Classify(1, status, false, now);
        Assert.False(result.Succeeded);
        Assert.True(result.Retry);
        Assert.Equal(now.AddSeconds(5), result.RetryAt);
    }

    [Fact]
    public void Retry_policy_stops_on_terminal_client_error()
    {
        var result = WebhookRetryPolicy.Classify(1, 422, false, DateTimeOffset.UtcNow);
        Assert.False(result.Succeeded);
        Assert.False(result.Retry);
        Assert.Equal("http_422", result.ErrorCode);
    }

    [Fact]
    public void Retry_policy_caps_retry_after_and_attempt_count()
    {
        var now = DateTimeOffset.Parse("2026-09-18T10:00:00Z");
        var delayed = WebhookRetryPolicy.Classify(3, 429, false, now, TimeSpan.FromDays(2));
        Assert.Equal(now.AddHours(6), delayed.RetryAt);

        var terminal = WebhookRetryPolicy.Classify(10, 503, false, now);
        Assert.False(terminal.Retry);
        Assert.Null(terminal.RetryAt);
    }
}
