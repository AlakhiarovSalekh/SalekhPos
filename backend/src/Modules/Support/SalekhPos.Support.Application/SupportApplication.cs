using SalekhPos.Support.Contracts;
using SalekhPos.Support.Domain.Diagnostics;
using SalekhPos.Support.Domain.Tickets;

namespace SalekhPos.Support.Application;

public sealed record SupportIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        SupportInput.Required(Issuer, 2048, nameof(Issuer));
        SupportInput.Required(Subject, 256, nameof(Subject));
    }
}

public static class SupportInput
{
    public static string Required(string? value, int maximum, string name)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length is < 1 || value.Length > maximum || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException("Support input is invalid.", name);
        return value;
    }
}

public sealed record CreateSupportTicketCommand(Guid OrganizationId, Guid TicketId, Guid OperationId,
    Guid? BranchId, string Subject, string Description, string Priority)
{
    public SupportTicket ToModel()
    {
        if (!Enum.TryParse<SupportTicketPriority>(Priority, true, out var priority))
            throw new ArgumentException("Ticket priority is invalid.", nameof(Priority));
        return new(OrganizationId, TicketId, BranchId, Subject, Description, priority);
    }
}

public sealed record AddDiagnosticReferenceCommand(Guid OrganizationId, Guid TicketId, Guid DiagnosticId,
    Guid OperationId, string Kind, string Reference, string Sha256)
{
    public DiagnosticReference ToModel() => new(Kind, Reference, Sha256);
}

public sealed record SupportWriteResult<T>(T Value, bool Created);

public interface ISupportService
{
    Task<SupportWriteResult<SupportTicketResponse>> CreateTicketAsync(SupportIdentity identity, CreateSupportTicketCommand command, CancellationToken cancellationToken);
    Task<SupportTicketPage> ListTicketsAsync(SupportIdentity identity, Guid organizationId, int pageSize, Guid? after, string? status, CancellationToken cancellationToken);
    Task<SupportTicketDetailResponse> GetTicketAsync(SupportIdentity identity, Guid organizationId, Guid ticketId, CancellationToken cancellationToken);
    Task<SupportTicketResponse> TransitionTicketAsync(SupportIdentity identity, Guid organizationId, Guid ticketId, Guid operationId, TransitionSupportTicketRequest request, CancellationToken cancellationToken);
    Task<SupportWriteResult<DiagnosticReferenceResponse>> AddDiagnosticAsync(SupportIdentity identity, AddDiagnosticReferenceCommand command, CancellationToken cancellationToken);
}

public sealed class SupportDeniedException : Exception;
public sealed class SupportConflictException : Exception;
public sealed class SupportNotFoundException : Exception;
public sealed class SupportUnavailableException : Exception;
