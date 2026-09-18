namespace SalekhPos.Desktop.Application.Management;

public sealed record SupportTicketSummary(Guid Id, Guid? BranchId, string Subject, string Description,
    string Priority, string Status, int Version, string OpenedBySubject,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SupportTicketPage(IReadOnlyList<SupportTicketSummary> Items, Guid? NextCursor);

public sealed record SupportDiagnostic(Guid Id, Guid TicketId, string Kind, string Reference,
    string Sha256, string AddedBySubject, DateTimeOffset CreatedAt);

public sealed record SupportTicketDetail(SupportTicketSummary Ticket, IReadOnlyList<SupportDiagnostic> Diagnostics);

public sealed record CreateSupportTicketInput(Guid? BranchId, string Subject, string Description, string Priority);
public sealed record TransitionSupportTicketInput(string Status, int ExpectedVersion, string Note);
public sealed record AddSupportDiagnosticInput(string Kind, string Reference, string Sha256);

public interface ISupportManager
{
    Task<SupportTicketPage> ListAsync(Guid organizationId, int pageSize, Guid? after,
        string? status, CancellationToken cancellationToken);
    Task<SupportTicketDetail> ReadAsync(Guid organizationId, Guid ticketId,
        CancellationToken cancellationToken);
    Task<SupportTicketSummary> CreateAsync(Guid organizationId, CreateSupportTicketInput input,
        Guid operationId, CancellationToken cancellationToken);
    Task<SupportTicketSummary> TransitionAsync(Guid organizationId, Guid ticketId,
        TransitionSupportTicketInput input, Guid operationId, CancellationToken cancellationToken);
    Task<SupportDiagnostic> AddDiagnosticAsync(Guid organizationId, Guid ticketId,
        AddSupportDiagnosticInput input, Guid operationId, CancellationToken cancellationToken);
}
