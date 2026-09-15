namespace SalekhPos.Audit.Contracts.AuditTrail;

public sealed record AuditEventResponse(Guid Id, long Sequence, string ActorSubject, string Action,
    string TargetType, Guid? TargetId, Guid? BranchId, Guid? DeviceId, string? SourceIp,
    string Outcome, string? Reason, string CorrelationId, string RequestId, DateTimeOffset OccurredAt,
    string PreviousHash, string EventHash);

public sealed record AuditEventPage(IReadOnlyList<AuditEventResponse> Items, long? NextSequence);

public sealed record AuditIntegrityResponse(bool IsValid, long VerifiedEvents,
    long? FirstSequence, long? LastSequence, string? LastHash);
