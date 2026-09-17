namespace SalekhPos.Support.Contracts;

public sealed record CreateSupportTicketRequest(Guid? BranchId, string Subject, string Description, string Priority);
public sealed record SupportTicketResponse(Guid Id, Guid? BranchId, string Subject, string Description,
    string Priority, string Status, int Version, string OpenedBySubject, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SupportTicketPage(IReadOnlyList<SupportTicketResponse> Items, Guid? NextCursor);
public sealed record TransitionSupportTicketRequest(string Status, int ExpectedVersion, string Note);
public sealed record AddDiagnosticReferenceRequest(string Kind, string Reference, string Sha256);
public sealed record DiagnosticReferenceResponse(Guid Id, Guid TicketId, string Kind, string Reference,
    string Sha256, string AddedBySubject, DateTimeOffset CreatedAt);
public sealed record SupportTicketDetailResponse(SupportTicketResponse Ticket,
    IReadOnlyList<DiagnosticReferenceResponse> Diagnostics);
