using System.Net;
using System.Text;
using SalekhPos.Desktop.Infrastructure.Management;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class NotificationDeliveryManagerHttpTests
{
    [Fact]
    public async Task Lists_filtered_delivery_activity()
    {
        var organizationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        using var client = Client(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains(
                $"/api/v1/organizations/{organizationId:D}/notifications/deliveries",
                request.RequestUri!.AbsolutePath,
                StringComparison.Ordinal);
            Assert.Contains("status=dead_lettered", request.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("channel=email", request.RequestUri.Query, StringComparison.Ordinal);
            return Task.FromResult(Json($$"""
                {"items":[{"id":"{{deliveryId:D}}","notificationId":"{{notificationId:D}}",
                "channel":"email","recipientSubject":"operator-1","status":"dead_lettered",
                "attemptCount":10,"nextAttemptAt":null,"lastErrorCode":"http_422",
                "createdAt":"2026-09-19T08:00:00Z","updatedAt":"2026-09-19T09:00:00Z",
                "title":"Provider alert","severity":"critical"}],"nextCursor":null}
                """));
        });

        var page = await new HttpNotificationDeliveryManager(client).DeliveriesAsync(
            organizationId,
            "dead_lettered",
            "email",
            default);

        var delivery = Assert.Single(page.Items);
        Assert.Equal(deliveryId, delivery.Id);
        Assert.Equal("dead_lettered", delivery.Status);
        Assert.Equal(10, delivery.AttemptCount);
    }

    [Fact]
    public async Task Retries_with_idempotency_key_and_audit_reason()
    {
        var organizationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        const string reason = "Provider configuration corrected.";

        using var client = Client(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/api/v1/organizations/{organizationId:D}/notifications/deliveries/{deliveryId:D}/retry",
                request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.TryGetValues("Idempotency-Key", out var values));
            Assert.Equal(operationId.ToString("D"), Assert.Single(values));
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains(reason, body, StringComparison.Ordinal);
            return Json($$"""
                {"id":"{{deliveryId:D}}","notificationId":"{{notificationId:D}}",
                "channel":"push","recipientSubject":"operator-1","status":"pending",
                "attemptCount":0,"nextAttemptAt":null,"lastErrorCode":null,
                "createdAt":"2026-09-19T08:00:00Z","updatedAt":"2026-09-19T09:10:00Z",
                "title":"Provider alert","severity":"warning"}
                """);
        });

        var result = await new HttpNotificationDeliveryManager(client).RetryAsync(
            organizationId,
            deliveryId,
            reason,
            operationId,
            default);

        Assert.Equal("pending", result.Status);
        Assert.Equal(0, result.AttemptCount);
        Assert.Null(result.LastErrorCode);
    }

    [Fact]
    public async Task Rejects_invalid_retry_response()
    {
        var organizationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        using var client = Client(_ => Task.FromResult(Json($$"""
            {"id":"{{deliveryId:D}}","notificationId":"{{notificationId:D}}",
            "channel":"email","recipientSubject":"operator-1","status":"pending",
            "attemptCount":1,"nextAttemptAt":null,"lastErrorCode":"should-be-cleared",
            "createdAt":"2026-09-19T08:00:00Z","updatedAt":"2026-09-19T09:10:00Z",
            "title":"Provider alert","severity":"warning"}
            """)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HttpNotificationDeliveryManager(client).RetryAsync(
                organizationId,
                deliveryId,
                "Safe retry.",
                Guid.NewGuid(),
                default));
    }

    private static HttpClient Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(new Handler(handler)) { BaseAddress = new Uri("https://pos.test/") };

    private static HttpResponseMessage Json(string value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json"),
        };

    private sealed class Handler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            await handler(request);
    }
}
