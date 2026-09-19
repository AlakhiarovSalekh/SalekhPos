using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationDeliveryManualRetryTests(AccessFixture fixture)
{
    [Fact]
    public async Task Dead_letter_retry_is_idempotent_audited_and_permission_scoped()
    {
        var organizationId = Guid.NewGuid();
        var subject = "notification-retry-" + Guid.NewGuid().ToString("N");
        await fixture.ExecuteAsync(
            "INSERT INTO organization.organizations(organization_id,name) VALUES($1,$2)",
            organizationId,
            "Notification retry " + organizationId.ToString("N"));
        await fixture.AddMembershipAsync(subject, organizationId, null);
        await fixture.GrantAsync(subject, organizationId, "notifications.manage");

        var notificationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.inbox(
              organization_id,notification_id,operation_id,branch_id,recipient_subject,title,body,severity)
            VALUES($1,$2,$3,NULL,$4,'Retry me','Provider delivery exhausted retries.','critical')
            """,
            organizationId,
            notificationId,
            Guid.NewGuid(),
            subject);
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.external_deliveries(
              organization_id,delivery_id,notification_id,channel,recipient_subject,status,
              attempt_count,last_error_code,next_attempt_at)
            VALUES($1,$2,$3,'email',$4,'dead_lettered',10,'http_422',NULL)
            """,
            organizationId,
            deliveryId,
            notificationId,
            subject);

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        var path =
            $"/api/v1/organizations/{organizationId:D}/notifications/deliveries/{deliveryId:D}/retry";
        var operationId = Guid.NewGuid();
        const string reason = "Provider configuration was corrected.";

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Retry(client, path, operationId, reason)));
        try
        {
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(deliveryId, json.RootElement.GetProperty("id").GetGuid());
                Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
                Assert.Equal(0, json.RootElement.GetProperty("attemptCount").GetInt32());
                Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("lastErrorCode").ValueKind);
                Assert.Equal("Retry me", json.RootElement.GetProperty("title").GetString());
            }
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        Assert.Equal(1L, await fixture.ScalarAsync<long>(
            """
            SELECT count(*)
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """,
            organizationId,
            operationId));
        Assert.Equal(10, await fixture.ScalarAsync<int>(
            """
            SELECT previous_attempt_count
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """,
            organizationId,
            operationId));
        Assert.Equal(reason, await fixture.ScalarAsync<string>(
            """
            SELECT reason
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """,
            organizationId,
            operationId));
        Assert.Equal(subject, await fixture.ScalarAsync<string>(
            """
            SELECT changed_by_subject
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND operation_id=$2
            """,
            organizationId,
            operationId));
        Assert.Equal("pending", await fixture.ScalarAsync<string>(
            """
            SELECT status
            FROM notifications.external_deliveries
            WHERE organization_id=$1 AND delivery_id=$2
            """,
            organizationId,
            deliveryId));
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            """
            SELECT attempt_count
            FROM notifications.external_deliveries
            WHERE organization_id=$1 AND delivery_id=$2
            """,
            organizationId,
            deliveryId));

        using var conflictingReplay = await Retry(
            client,
            path,
            operationId,
            "Different retry intent.");
        Assert.Equal(HttpStatusCode.Conflict, conflictingReplay.StatusCode);

        using var secondOperation = await Retry(
            client,
            path,
            Guid.NewGuid(),
            reason);
        Assert.Equal(HttpStatusCode.Conflict, secondOperation.StatusCode);

        using var invalidReason = await Retry(
            client,
            path,
            Guid.NewGuid(),
            "   ");
        Assert.Equal(HttpStatusCode.BadRequest, invalidReason.StatusCode);

        using var missingOperation = await client.PostAsJsonAsync(
            path,
            new { Reason = "Missing idempotency key." });
        Assert.Equal(HttpStatusCode.BadRequest, missingOperation.StatusCode);
    }

    [Fact]
    public async Task Retry_requires_notification_management_permission()
    {
        var organizationId = Guid.NewGuid();
        var subject = "notification-retry-viewer-" + Guid.NewGuid().ToString("N");
        await fixture.ExecuteAsync(
            "INSERT INTO organization.organizations(organization_id,name) VALUES($1,$2)",
            organizationId,
            "Notification retry viewer " + organizationId.ToString("N"));
        await fixture.AddMembershipAsync(subject, organizationId, null);
        await fixture.GrantAsync(subject, organizationId, "notifications.view");

        var notificationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.inbox(
              organization_id,notification_id,operation_id,recipient_subject,title,body,severity)
            VALUES($1,$2,$3,$4,'Denied retry','No management permission.','warning')
            """,
            organizationId,
            notificationId,
            Guid.NewGuid(),
            subject);
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.external_deliveries(
              organization_id,delivery_id,notification_id,channel,recipient_subject,status,
              attempt_count,last_error_code)
            VALUES($1,$2,$3,'push',$4,'dead_lettered',10,'provider_rejected')
            """,
            organizationId,
            deliveryId,
            notificationId,
            subject);

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        using var response = await Retry(
            client,
            $"/api/v1/organizations/{organizationId:D}/notifications/deliveries/{deliveryId:D}/retry",
            Guid.NewGuid(),
            "Operator requested retry.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await fixture.ScalarAsync<long>(
            """
            SELECT count(*)
            FROM notifications.delivery_manual_retries
            WHERE organization_id=$1 AND delivery_id=$2
            """,
            organizationId,
            deliveryId));
    }

    private static async Task<HttpResponseMessage> Retry(
        HttpClient client,
        string path,
        Guid operationId,
        string reason)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { Reason = reason }),
        };
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            operationId.ToString("D"));
        return await client.SendAsync(request);
    }
}
