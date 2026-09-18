using System.Text.Json;
using SalekhPos.Integrations.Domain.Security;
using SalekhPos.Integrations.Infrastructure.Webhooks;
using SalekhPos.Notifications.Contracts.Notifications;
using SalekhPos.Notifications.Domain.Notifications;

namespace SalekhPos.Worker.Notifications;

public sealed record NotificationTransportResult(
    bool Succeeded,
    int? StatusCode,
    string? ErrorCode,
    DateTimeOffset? RetryAt)
{
    public RecordNotificationDeliveryAttemptRequest ToAttempt(Guid leaseId) =>
        new(leaseId, Succeeded, ErrorCode, RetryAt);
}

public sealed class NotificationProviderTransport(
    Microsoft.Extensions.Options.IOptions<NotificationDispatchOptions> options,
    IWebhookHttpSender sender,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly NotificationDispatchOptions settings = options.Value;

    public bool IsReady =>
        ReadCredential(settings.EmailCredentialEnvironmentVariable) is not null
        && ReadCredential(settings.PushCredentialEnvironmentVariable) is not null;

    public async Task<NotificationTransportResult> DispatchAsync(
        LeasedNotificationDeliveryResponse lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.LeaseId == Guid.Empty || lease.Delivery.Id == Guid.Empty || lease.Delivery.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification delivery lease identity is invalid.", nameof(lease));
        }

        var channel = lease.Delivery.Channel.Trim().ToLowerInvariant();
        var endpointText = channel switch
        {
            "email" => settings.EmailEndpoint,
            "push" => settings.PushEndpoint,
            _ => throw new ArgumentException("Notification delivery channel is invalid.", nameof(lease)),
        };
        var credentialVariable = channel switch
        {
            "email" => settings.EmailCredentialEnvironmentVariable,
            "push" => settings.PushCredentialEnvironmentVariable,
            _ => throw new ArgumentException("Notification delivery channel is invalid.", nameof(lease)),
        };

        var now = timeProvider.GetUtcNow();
        var credential = ReadCredential(credentialVariable);
        if (credential is null)
        {
            var unavailable = NotificationDeliveryRetryPolicy.Classify(
                lease.Delivery.AttemptCount + 1,
                null,
                true,
                now);
            return new(false, null, unavailable.ErrorCode, unavailable.RetryAt);
        }

        var endpoint = WebhookEndpointPolicy.ValidateUri(endpointText);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new ProviderPayload(
            lease.Delivery.Id,
            lease.Delivery.NotificationId,
            channel,
            lease.Delivery.RecipientSubject,
            lease.Title,
            lease.Body,
            lease.Severity), SerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.UserAgent.ParseAdd("SalekhPos-Notification/1.0");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential);
        request.Headers.TryAddWithoutValidation("X-SalekhPos-Delivery", lease.Delivery.Id.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-SalekhPos-Notification", lease.Delivery.NotificationId.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-SalekhPos-Channel", channel);
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new("application/json");

        try
        {
            using var response = await sender.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;
            var decision = NotificationDeliveryRetryPolicy.Classify(
                lease.Delivery.AttemptCount + 1,
                statusCode,
                false,
                now,
                RetryAfter(response, now));
            return new(decision.Succeeded, statusCode, decision.ErrorCode, decision.RetryAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            var decision = NotificationDeliveryRetryPolicy.Classify(
                lease.Delivery.AttemptCount + 1,
                null,
                true,
                now);
            return new(false, null, decision.ErrorCode, decision.RetryAt);
        }
    }

    private static string? ReadCredential(string variableName)
    {
        variableName = variableName?.Trim() ?? string.Empty;
        if (variableName.Length is < 1 or > 128) return null;

        var value = Environment.GetEnvironmentVariable(variableName)?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length is < 20 or > 4096
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            return null;
        }

        return value;
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var value = response.Headers.RetryAfter;
        if (value?.Delta is { } delta && delta > TimeSpan.Zero) return delta;
        if (value?.Date is { } date && date > now) return date - now;
        return null;
    }

    private sealed record ProviderPayload(
        Guid DeliveryId,
        Guid NotificationId,
        string Channel,
        string RecipientSubject,
        string Title,
        string Body,
        string Severity);
}
