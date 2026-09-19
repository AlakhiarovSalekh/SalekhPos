using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpNotificationDeliveryManager(HttpClient client) : INotificationDeliveryManager
{
    private const int MaximumPageSize = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<DesktopNotificationDeliveryPage> DeliveriesAsync(
        Guid organizationId,
        string? status,
        string? channel,
        CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        status = Optional(status, ["pending", "delivering", "failed", "delivered", "dead_lettered"], "status");
        channel = Optional(channel, ["email", "push"], "channel");
        var query = new List<string> { $"pageSize={MaximumPageSize}" };
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (channel is not null) query.Add($"channel={Uri.EscapeDataString(channel)}");
        var path = $"api/v1/organizations/{organizationId:D}/notifications/deliveries?{string.Join("&", query)}";

        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<DesktopNotificationDeliveryPage>(Json, cancellationToken)
            ?? throw InvalidResponse();
        if (page.Items is null || page.Items.Count > MaximumPageSize) throw InvalidResponse();
        foreach (var item in page.Items) Validate(item);
        if (page.NextCursor == Guid.Empty) throw InvalidResponse();
        return page;
    }

    public async Task<DesktopNotificationDelivery> RetryAsync(
        Guid organizationId,
        Guid deliveryId,
        string reason,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        if (deliveryId == Guid.Empty || operationId == Guid.Empty)
            throw new ArgumentException("Delivery and operation are required.");
        reason = Required(reason, 500, "Retry reason");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/organizations/{organizationId:D}/notifications/deliveries/{deliveryId:D}/retry")
        {
            Content = JsonContent.Create(new { Reason = reason }, options: Json),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var delivery = await response.Content.ReadFromJsonAsync<DesktopNotificationDelivery>(Json, cancellationToken)
            ?? throw InvalidResponse();
        Validate(delivery);
        if (delivery.Id != deliveryId || delivery.Status != "pending" || delivery.AttemptCount != 0
            || delivery.LastErrorCode is not null || delivery.NextAttemptAt is not null)
            throw InvalidResponse();
        return delivery;
    }

    private static void Validate(DesktopNotificationDelivery value)
    {
        if (value.Id == Guid.Empty || value.NotificationId == Guid.Empty
            || value.Channel is not ("email" or "push")
            || value.Status is not ("pending" or "delivering" or "failed" or "delivered" or "dead_lettered")
            || value.AttemptCount is < 0 or > 10
            || InvalidText(value.RecipientSubject, 256)
            || InvalidText(value.Title, 160)
            || value.Severity is not ("info" or "warning" or "critical")
            || value.LastErrorCode is { } error && InvalidText(error, 100)
            || value.CreatedAt == default || value.UpdatedAt == default)
        {
            throw InvalidResponse();
        }
    }

    private static string? Optional(string? value, string[] allowed, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized, StringComparer.Ordinal) ? normalized
            : throw new ArgumentException($"{label} is invalid.");
    }

    private static string Required(string value, int maximum, string label)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 1 || normalized.Length > maximum || normalized.Any(char.IsControl))
            throw new ArgumentException($"{label} is invalid.");
        return normalized;
    }

    private static bool InvalidText(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximum
        || value.Any(char.IsControl);

    private static void ValidateOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
    }

    private static InvalidOperationException InvalidResponse() =>
        new("Notification delivery response is invalid.");
}
