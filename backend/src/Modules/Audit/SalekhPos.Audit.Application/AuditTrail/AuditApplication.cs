using SalekhPos.Audit.Contracts.AuditTrail;
using SalekhPos.Audit.Domain.AuditEvents;

namespace SalekhPos.Audit.Application.AuditTrail;

public sealed record AuditIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Audit identity is invalid.");
    }
}

public sealed record AppendAuditCommand(AuditEventDraft Event);
public sealed record AuditAppendResult(AuditEventResponse Event, bool Created);

public interface IAuditTrail
{
    Task<AuditAppendResult> AppendAsync(AuditIdentity identity, AppendAuditCommand command, CancellationToken ct);
    Task<AuditEventPage> ListAsync(AuditIdentity identity, Guid organizationId, int pageSize,
        long? afterSequence, string? action, Guid? branchId, CancellationToken ct);
    Task<AuditEventResponse?> ReadAsync(AuditIdentity identity, Guid organizationId,
        Guid eventId, CancellationToken ct);
    Task<AuditIntegrityResponse> VerifyAsync(AuditIdentity identity, Guid organizationId,
        long? fromSequence, int limit, CancellationToken ct);
}

public sealed class AuditDeniedException : Exception;
public sealed class AuditConflictException : Exception;
public sealed class AuditUnavailableException : Exception;
