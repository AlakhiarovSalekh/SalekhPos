using SalekhPos.Support.Application;
using SalekhPos.Support.Domain.Diagnostics;
using SalekhPos.Support.Domain.Tickets;

namespace SalekhPos.Tests.Support;

public sealed class SupportDomainTests
{
    [Fact]
    public void Ticket_trims_and_retains_bounded_customer_content()
    {
        var ticket = new SupportTicket(Guid.NewGuid(), Guid.NewGuid(), null, "  Printer unavailable  ",
            "The receipt printer is offline.", SupportTicketPriority.High);
        Assert.Equal("Printer unavailable", ticket.Subject); Assert.Equal(SupportTicketStatus.Open, ticket.Status);
    }

    [Theory]
    [InlineData(SupportTicketStatus.InProgress)]
    [InlineData(SupportTicketStatus.Closed)]
    public void Open_ticket_allows_documented_transitions(SupportTicketStatus next)
    {
        var ticket = Ticket(); Assert.Equal(next, ticket.TransitionTo(next).Status);
    }

    [Theory]
    [InlineData(SupportTicketStatus.WaitingForCustomer)]
    [InlineData(SupportTicketStatus.Resolved)]
    public void Open_ticket_rejects_skipped_transitions(SupportTicketStatus next) =>
        Assert.Throws<InvalidOperationException>(() => Ticket().TransitionTo(next));

    [Fact]
    public void Closed_ticket_is_terminal() =>
        Assert.Throws<InvalidOperationException>(() => Ticket().TransitionTo(SupportTicketStatus.Closed).TransitionTo(SupportTicketStatus.InProgress));

    [Fact]
    public void Diagnostic_reference_requires_content_digest() =>
        Assert.Throws<ArgumentException>(() => new DiagnosticReference("log", "object://diagnostics/1", "not-a-digest"));

    [Fact]
    public void Diagnostic_reference_normalizes_kind_and_digest()
    {
        var reference = new DiagnosticReference(" LOG ", "object://diagnostics/1", new string('A', 64));
        Assert.Equal("log", reference.Kind); Assert.Equal(new string('a', 64), reference.Sha256);
    }

    [Fact]
    public void Create_command_rejects_unknown_priority() =>
        Assert.Throws<ArgumentException>(() => new CreateSupportTicketCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, "Issue", "Description", "blocker").ToModel());

    [Fact]
    public void Ticket_rejects_oversized_description() =>
        Assert.Throws<ArgumentException>(() => new SupportTicket(Guid.NewGuid(), Guid.NewGuid(), null, "Issue",
            new string('x', 8001), SupportTicketPriority.Normal));

    private static SupportTicket Ticket() => new(Guid.NewGuid(), Guid.NewGuid(), null, "Issue", "Description", SupportTicketPriority.Normal);
}
