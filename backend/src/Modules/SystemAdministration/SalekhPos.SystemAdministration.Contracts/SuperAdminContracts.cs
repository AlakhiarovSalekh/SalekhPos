namespace SalekhPos.SystemAdministration.Contracts;

public sealed record RegisterSuperAdminRequest(Guid OperationId, string Subject, string Reason);
public sealed record RevokeSuperAdminRequest(Guid OperationId, string Reason);
public sealed record PlatformAuthority(bool IsRoot, bool IsSuperAdmin);
public sealed record SuperAdminResponse(Guid Id, string Issuer, string Subject, bool IsRoot,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

public sealed record SuperAdminPage(IReadOnlyList<SuperAdminResponse> Items, Guid? NextCursor);
public sealed record PlatformAuthorityAuditResponse(Guid OperationId, string Action, string ActorSubject,
    string TargetKey, Guid TargetId, string Reason, string TraceId, DateTimeOffset RecordedAt);
public sealed record PlatformAuthorityAuditPage(IReadOnlyList<PlatformAuthorityAuditResponse> Items, Guid? NextCursor);
