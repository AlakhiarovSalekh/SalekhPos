using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationDeliveryVisibilityTests(AccessFixture fixture)
{
    [Fact]
    public async Task Manage_permission_can_read_tenant_delivery_activity_but_view_only_cannot()
    {
        var organizationId = Guid.NewGuid();
        var manager = "notification-manager-" + Guid.NewGuid().ToString("N");
        var viewer = "notification-viewer-" + Guid.NewGuid().ToString("N");

        await fixture.ExecuteAsync(
            "INSERT INTO organization.organizations(organization_id,name) VALUES($1,$2)",
            organizationId,
            "Notification delivery visibility " + organizationId.ToString("N"));
        await fixture.AddMembershipAsync(manager, organizationId, null);
        await fixture.AddMembershipAsync(viewer, organizationId, null);
        await fixture.GrantAsync(manager, organizationId, "notifications.view");
        await fixture.GrantAsync(manager, organizationId, "notifications.manage");
        await fixture.GrantAsync(viewer, organizationId, "notifications.view");

        var notificationId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.inbox(
              organization_id,notification_id,operation_id,branch_id,recipient_subject,title,body,severity)
            VALUES($1,$2,$3,NULL,$4,'External delivery visibility',
              'This alert has an email delivery record.','warning')
            """,
            organizationId,
            notificationId,
            Guid.NewGuid(),
            manager);
        await fixture.ExecuteAsync(
            """
            INSERT INTO notifications.external_deliveries(
              organization_id,delivery_id,notification_id,channel,recipient_subject,status)
            VALUES($1,$2,$3,'email',$4,'pending')
            """,
            organizationId,
            deliveryId,
            notificationId,
            manager);

        using var managerClient = fixture.Factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token(manager));
        var root = $"/api/v1/organizations/{organizationId:D}/notifications";

        using var list = await managerClient.GetAsync(
            root + "/deliveries?pageSize=25&status=pending&channel=email");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = page.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var item = Assert.Single(items);
        Assert.Equal("email", item.GetProperty("channel").GetString());
        Assert.Equal("pending", item.GetProperty("status").GetString());
        Assert.Equal(manager, item.GetProperty("recipientSubject").GetString());
        Assert.Equal(0, item.GetProperty("attemptCount").GetInt32());
        Assert.Equal("External delivery visibility", item.GetProperty("title").GetString());
        Assert.False(item.TryGetProperty("providerSecret", out _));
        Assert.False(item.TryGetProperty("destination", out _));

        using var viewerClient = fixture.Factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token(viewer));

        using var inbox = await viewerClient.GetAsync(root + "?pageSize=25");
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);

        using var forbidden = await viewerClient.GetAsync(root + "/deliveries?pageSize=25");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var problem = JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync());
        Assert.Equal(
            "notification_access_denied",
            problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delivery_activity_query_rejects_unknown_filters()
    {
        var organizationId = Guid.NewGuid();
        var subject = "notification-filter-" + Guid.NewGuid().ToString("N");

        await fixture.ExecuteAsync(
            "INSERT INTO organization.organizations(organization_id,name) VALUES($1,$2)",
            organizationId,
            "Notification delivery filters " + organizationId.ToString("N"));
        await fixture.AddMembershipAsync(subject, organizationId, null);
        await fixture.GrantAsync(subject, organizationId, "notifications.manage");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token(subject));

        using var unknownKey = await client.GetAsync(
            $"/api/v1/organizations/{organizationId:D}/notifications/deliveries?pageSize=25&secret=true");
        Assert.Equal(HttpStatusCode.BadRequest, unknownKey.StatusCode);

        using var invalidStatus = await client.GetAsync(
            $"/api/v1/organizations/{organizationId:D}/notifications/deliveries?status=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);

        using var invalidChannel = await client.GetAsync(
            $"/api/v1/organizations/{organizationId:D}/notifications/deliveries?channel=sms");
        Assert.Equal(HttpStatusCode.BadRequest, invalidChannel.StatusCode);
    }

}
