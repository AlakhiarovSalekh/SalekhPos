namespace SalekhPos.Notifications.Domain.Notifications;

public enum NotificationSeverity { Info, Warning, Critical }

public sealed record Notification
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid? BranchId { get; }
    public string RecipientSubject { get; }
    public string Title { get; }
    public string Body { get; }
    public NotificationSeverity Severity { get; }

    public Notification(Guid organizationId, Guid id, Guid? branchId, string recipientSubject,
        string title, string body, NotificationSeverity severity)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Notification identity is invalid.");
        OrganizationId = organizationId; Id = id; BranchId = branchId;
        RecipientSubject = Text(recipientSubject, 256, nameof(recipientSubject));
        Title = Text(title, 160, nameof(title)); Body = Text(body, 2000, nameof(body)); Severity = severity;
    }

    private static string Text(string value, int max, string name)
    {
        value = value?.Trim() ?? "";
        if (value.Length is < 1 || value.Length > max || value.Any(char.IsControl)) throw new ArgumentException("Notification text is invalid.", name);
        return value;
    }
}
