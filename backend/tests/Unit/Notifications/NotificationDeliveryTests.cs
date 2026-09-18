using SalekhPos.Notifications.Domain.Notifications;

namespace SalekhPos.Tests.Notifications;

public sealed class NotificationDeliveryTests
{
    [Fact]
    public void Delivery_requires_bounded_identity_and_attempt_count()
    {
        var organizationId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        var delivery = new NotificationDelivery(
            organizationId,
            Guid.NewGuid(),
            notificationId,
            NotificationDeliveryChannel.Email,
            "recipient",
            NotificationDeliveryStatus.Pending,
            0);

        Assert.Equal(organizationId, delivery.OrganizationId);
        Assert.Equal(notificationId, delivery.NotificationId);
        Assert.Equal(NotificationDeliveryChannel.Email, delivery.Channel);
        Assert.Equal(NotificationDeliveryStatus.Pending, delivery.Status);

        Assert.Throws<ArgumentException>(() => new NotificationDelivery(
            Guid.Empty, Guid.NewGuid(), notificationId, NotificationDeliveryChannel.Email,
            "recipient", NotificationDeliveryStatus.Pending, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationDelivery(
            organizationId, Guid.NewGuid(), notificationId, NotificationDeliveryChannel.Push,
            "recipient", NotificationDeliveryStatus.Failed, 11));
    }
}
