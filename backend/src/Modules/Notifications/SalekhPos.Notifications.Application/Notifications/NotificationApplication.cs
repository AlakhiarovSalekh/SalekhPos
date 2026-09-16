using SalekhPos.Notifications.Contracts.Notifications;
using SalekhPos.Notifications.Domain.Notifications;

namespace SalekhPos.Notifications.Application.Notifications;

public sealed record NotificationIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Notification identity is invalid.");
    }
}

public sealed record CreateNotificationCommand(Guid OrganizationId, Guid NotificationId, Guid OperationId,
    Guid? BranchId, string RecipientSubject, string Title, string Body, string Severity)
{
    public Notification ToNotification()
    {
        if (!Enum.TryParse<NotificationSeverity>(Severity, true, out var severity)) throw new ArgumentException("Severity is invalid.");
        return new(OrganizationId, NotificationId, BranchId, RecipientSubject, Title, Body, severity);
    }
}
public sealed record NotificationWriteResult(NotificationResponse Notification, bool Created);

public interface INotificationCenter
{
    Task<NotificationWriteResult> CreateAsync(NotificationIdentity identity, CreateNotificationCommand command, CancellationToken ct);
    Task<NotificationPage> ListMineAsync(NotificationIdentity identity, Guid organizationId, int pageSize,
        Guid? after, bool unreadOnly, CancellationToken ct);
    Task<NotificationResponse> MarkReadAsync(NotificationIdentity identity, Guid organizationId, Guid notificationId, CancellationToken ct);
    Task<NotificationPreferencesResponse> ReadPreferencesAsync(NotificationIdentity identity, Guid organizationId, CancellationToken ct);
    Task<NotificationPreferencesResponse> UpdatePreferencesAsync(NotificationIdentity identity, Guid organizationId,
        UpdateNotificationPreferencesRequest request, CancellationToken ct);
}

public sealed class NotificationDeniedException : Exception;
public sealed class NotificationUnavailableException : Exception;
public sealed class NotificationConflictException : Exception;
public sealed class NotificationNotFoundException : Exception;
