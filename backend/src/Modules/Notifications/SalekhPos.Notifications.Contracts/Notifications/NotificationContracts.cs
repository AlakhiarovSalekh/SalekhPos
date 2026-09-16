namespace SalekhPos.Notifications.Contracts.Notifications;

public sealed record CreateNotificationRequest(Guid? BranchId, string RecipientSubject,
    string Title, string Body, string Severity);
public sealed record NotificationResponse(Guid Id, Guid? BranchId, string RecipientSubject,
    string Title, string Body, string Severity, bool IsRead, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPage(IReadOnlyList<NotificationResponse> Items, Guid? NextCursor);
public sealed record NotificationPreferencesResponse(bool InAppEnabled, bool EmailEnabled, bool PushEnabled,
    DateTimeOffset UpdatedAt);
public sealed record UpdateNotificationPreferencesRequest(bool InAppEnabled, bool EmailEnabled, bool PushEnabled);
