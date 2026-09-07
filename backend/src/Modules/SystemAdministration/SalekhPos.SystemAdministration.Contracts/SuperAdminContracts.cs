namespace SalekhPos.SystemAdministration.Contracts;

public sealed record RegisterSuperAdminRequest(Guid OperationId, string Subject, string Reason);
public sealed record RevokeSuperAdminRequest(Guid OperationId, string Reason);
public sealed record PlatformAuthority(bool IsRoot, bool IsSuperAdmin);
public sealed record SuperAdminResponse(Guid Id, string Issuer, string Subject, bool IsRoot,
    bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
