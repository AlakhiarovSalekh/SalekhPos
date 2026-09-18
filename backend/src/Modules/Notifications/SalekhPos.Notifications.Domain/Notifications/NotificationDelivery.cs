namespace SalekhPos.Notifications.Domain.Notifications;

public enum NotificationDeliveryChannel
{
    Email,
    Push
}

public enum NotificationDeliveryStatus
{
    Pending,
    Delivering,
    Failed,
    Delivered,
    DeadLettered
}

public sealed record NotificationDelivery
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid NotificationId { get; }
    public NotificationDeliveryChannel Channel { get; }
    public string RecipientSubject { get; }
    public NotificationDeliveryStatus Status { get; }
    public int AttemptCount { get; }

    public NotificationDelivery(
        Guid organizationId,
        Guid id,
        Guid notificationId,
        NotificationDeliveryChannel channel,
        string recipientSubject,
        NotificationDeliveryStatus status,
        int attemptCount)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification delivery identity is invalid.");
        }

        recipientSubject = recipientSubject?.Trim() ?? string.Empty;
        if (recipientSubject.Length is < 1 or > 256 || recipientSubject.Any(char.IsControl))
        {
            throw new ArgumentException("Notification recipient is invalid.", nameof(recipientSubject));
        }

        if (attemptCount is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        }

        OrganizationId = organizationId;
        Id = id;
        NotificationId = notificationId;
        Channel = channel;
        RecipientSubject = recipientSubject;
        Status = status;
        AttemptCount = attemptCount;
    }
}
