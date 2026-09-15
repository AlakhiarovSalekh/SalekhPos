namespace SalekhPos.Desktop.Application.Management;

public sealed record AuditEventSummary(Guid Id, long Sequence, string ActorSubject, string Action,
    string TargetType, Guid? TargetId, Guid? BranchId, Guid? DeviceId, string? SourceIp,
    string Outcome, string? Reason, string CorrelationId, string RequestId, DateTimeOffset OccurredAt,
    string PreviousHash, string EventHash);
public sealed record AuditEventPage(IReadOnlyList<AuditEventSummary> Items, long? NextSequence);
public sealed record AuditIntegritySummary(bool IsValid, long VerifiedEvents, long? FirstSequence,
    long? LastSequence, string? LastHash);

public interface IAuditViewer
{
    Task<AuditEventPage> ListAsync(Guid organizationId, int pageSize, long? afterSequence,
        string? action, Guid? branchId, CancellationToken cancellationToken);
    Task<AuditIntegritySummary> VerifyAsync(Guid organizationId, long? fromSequence,
        int limit, CancellationToken cancellationToken);
}
