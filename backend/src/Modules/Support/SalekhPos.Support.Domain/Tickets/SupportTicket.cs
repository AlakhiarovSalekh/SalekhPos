namespace SalekhPos.Support.Domain.Tickets;

public enum SupportTicketPriority { Low, Normal, High, Urgent }
public enum SupportTicketStatus { Open, InProgress, WaitingForCustomer, Resolved, Closed }

public sealed record SupportTicket
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid? BranchId { get; }
    public string Subject { get; }
    public string Description { get; }
    public SupportTicketPriority Priority { get; }
    public SupportTicketStatus Status { get; }

    public SupportTicket(Guid organizationId, Guid id, Guid? branchId, string subject, string description,
        SupportTicketPriority priority, SupportTicketStatus status = SupportTicketStatus.Open)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Ticket identity is invalid.");
        OrganizationId = organizationId; Id = id; BranchId = branchId;
        Subject = SupportText.Required(subject, 200, nameof(subject));
        Description = SupportText.Required(description, 8000, nameof(description));
        Priority = priority; Status = status;
    }

    public SupportTicket TransitionTo(SupportTicketStatus next)
    {
        var allowed = Status switch
        {
            SupportTicketStatus.Open => next is SupportTicketStatus.InProgress or SupportTicketStatus.Closed,
            SupportTicketStatus.InProgress => next is SupportTicketStatus.WaitingForCustomer or SupportTicketStatus.Resolved or SupportTicketStatus.Closed,
            SupportTicketStatus.WaitingForCustomer => next is SupportTicketStatus.InProgress or SupportTicketStatus.Resolved or SupportTicketStatus.Closed,
            SupportTicketStatus.Resolved => next is SupportTicketStatus.InProgress or SupportTicketStatus.Closed,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException("Ticket status transition is not allowed.");
        return new(OrganizationId, Id, BranchId, Subject, Description, Priority, next);
    }
}

internal static class SupportText
{
    internal static string Required(string? value, int maximum, string name)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length is < 1 || value.Length > maximum || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException("Support text is invalid.", name);
        return value;
    }
}
