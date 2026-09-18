using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using SalekhPos.Integrations.Contracts;
using SalekhPos.Integrations.Domain.Security;
using SalekhPos.Integrations.Domain.Webhooks;

namespace SalekhPos.Integrations.Infrastructure.Webhooks;

public interface IWebhookPayloadResolver
{
    ValueTask<ReadOnlyMemory<byte>> ResolveAsync(string reference, CancellationToken cancellationToken);
}

public interface IWebhookSecretResolver
{
    ValueTask<byte[]> ResolveAsync(string reference, CancellationToken cancellationToken);
}

public interface IWebhookHttpSender
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

public sealed class WebhookResolverUnavailableException(string message) : Exception(message);

public sealed record WebhookTransportResult(bool Succeeded, int? StatusCode, string? ErrorCode, DateTimeOffset? RetryAt)
{
    public RecordWebhookAttemptRequest ToAttempt(Guid leaseId) =>
        new(leaseId, Succeeded, StatusCode, ErrorCode, RetryAt);
}

public sealed class WebhookTransport(
    IWebhookPayloadResolver payloads,
    IWebhookSecretResolver secrets,
    IWebhookHttpSender sender,
    TimeProvider timeProvider)
{
    public const int MaximumPayloadBytes = 262_144;

    public async Task<WebhookTransportResult> DispatchAsync(LeasedWebhookResponse lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.LeaseId == Guid.Empty || lease.Delivery.Id == Guid.Empty)
            throw new ArgumentException("Webhook lease identity is invalid.", nameof(lease));

        var endpoint = WebhookEndpointPolicy.ValidateUri(lease.Endpoint);
        var payload = await payloads.ResolveAsync(Required(lease.PayloadReference, 512), cancellationToken);
        if (payload.Length is < 1 or > MaximumPayloadBytes)
            return new(false, null, "payload_size_invalid", null);
        if (!DigestMatches(payload.Span, lease.Delivery.PayloadSha256))
            return new(false, null, "payload_digest_mismatch", null);

        var secret = await secrets.ResolveAsync(Required(lease.SecretReference, 512), cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var timestamp = now.ToUnixTimeSeconds();
            var signature = WebhookSignature.Sign(secret, timestamp, payload.Span);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.UserAgent.ParseAdd("SalekhPos-Webhook/1.0");
            request.Headers.TryAddWithoutValidation("X-SalekhPos-Timestamp",
                timestamp.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("X-SalekhPos-Delivery", lease.Delivery.Id.ToString("D"));
            request.Headers.TryAddWithoutValidation("X-SalekhPos-Event", lease.Delivery.EventId.ToString("D"));
            request.Headers.TryAddWithoutValidation("X-SalekhPos-Signature", signature);
            request.Content = new ByteArrayContent(payload.ToArray());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                using var response = await sender.SendAsync(request, cancellationToken);
                var status = (int)response.StatusCode;
                var decision = WebhookRetryPolicy.Classify(lease.Delivery.AttemptCount + 1, status, false, now,
                    RetryAfter(response, now));
                return new(decision.Succeeded, status, decision.ErrorCode, decision.RetryAt);
            }
            catch (HttpRequestException)
            {
                var decision = WebhookRetryPolicy.Classify(lease.Delivery.AttemptCount + 1, null, true, now);
                return new(false, null, decision.ErrorCode, decision.RetryAt);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var decision = WebhookRetryPolicy.Classify(lease.Delivery.AttemptCount + 1, null, true, now);
                return new(false, null, decision.ErrorCode, decision.RetryAt);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static bool DigestMatches(ReadOnlySpan<byte> payload, string digest)
    {
        if (digest.Length != 64) return false;
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(digest);
        }
        catch (FormatException)
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[32];
        SHA256.HashData(payload, actual);
        return expected.Length == 32 && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var value = response.Headers.RetryAfter;
        if (value?.Delta is { } delta && delta > TimeSpan.Zero) return delta;
        if (value?.Date is { } date && date > now) return date - now;
        return null;
    }

    private static string Required(string? value, int maximum)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length is < 1 || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Webhook transport reference is invalid.");
        return value;
    }
}
