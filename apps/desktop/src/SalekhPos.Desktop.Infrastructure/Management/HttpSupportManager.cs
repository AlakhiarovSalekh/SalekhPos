using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpSupportManager(HttpClient client) : ISupportManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Priorities = ["low", "normal", "high", "urgent"];
    private static readonly HashSet<string> Statuses = ["open", "in_progress", "waiting_for_customer", "resolved", "closed"];

    public async Task<SupportTicketPage> ListAsync(Guid organizationId, int pageSize, Guid? after,
        string? status, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId);
        if (pageSize is < 1 or > 100 || after == Guid.Empty || status is not null && !Statuses.Contains(status))
            throw new ArgumentException("Support query is invalid.");
        var path = $"api/v1/organizations/{organizationId:D}/support/tickets?pageSize={pageSize}";
        if (after.HasValue) path += $"&after={after.Value:D}";
        if (status is not null) path += $"&status={Uri.EscapeDataString(status)}";
        var page = await Get<SupportTicketPage>(path, cancellationToken);
        if (page.Items.Count > pageSize || page.NextCursor == Guid.Empty)
            throw new InvalidOperationException("Support ticket page is invalid.");
        foreach (var ticket in page.Items) ValidateTicket(ticket);
        return page;
    }

    public async Task<SupportTicketDetail> ReadAsync(Guid organizationId, Guid ticketId,
        CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateId(ticketId, nameof(ticketId));
        var detail = await Get<SupportTicketDetail>(
            $"api/v1/organizations/{organizationId:D}/support/tickets/{ticketId:D}", cancellationToken);
        ValidateTicket(detail.Ticket);
        if (detail.Ticket.Id != ticketId || detail.Diagnostics.Count > 100)
            throw new InvalidOperationException("Support detail identity is invalid.");
        foreach (var diagnostic in detail.Diagnostics) ValidateDiagnostic(diagnostic, ticketId);
        return detail;
    }

    public async Task<SupportTicketSummary> CreateAsync(Guid organizationId,
        CreateSupportTicketInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateOperation(operationId);
        if (input.BranchId == Guid.Empty || !ValidText(input.Subject, 200)
            || !ValidText(input.Description, 8000, true) || !Priorities.Contains(input.Priority))
            throw new ArgumentException("Support ticket input is invalid.");
        var ticket = await Post<SupportTicketSummary>(
            $"api/v1/organizations/{organizationId:D}/support/tickets",
            new { input.BranchId, input.Subject, input.Description, input.Priority }, operationId, cancellationToken);
        ValidateTicket(ticket);
        if (ticket.BranchId != input.BranchId || ticket.Subject != input.Subject.Trim()
            || ticket.Description != input.Description.Trim() || ticket.Priority != input.Priority)
            throw new InvalidOperationException("Support ticket response does not match the request.");
        return ticket;
    }

    public async Task<SupportTicketSummary> TransitionAsync(Guid organizationId, Guid ticketId,
        TransitionSupportTicketInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateId(ticketId, nameof(ticketId)); ValidateOperation(operationId);
        if (!Statuses.Contains(input.Status) || input.ExpectedVersion < 0 || !ValidText(input.Note, 2000, true))
            throw new ArgumentException("Support transition is invalid.");
        var ticket = await Post<SupportTicketSummary>(
            $"api/v1/organizations/{organizationId:D}/support/tickets/{ticketId:D}/transitions",
            new { input.Status, input.ExpectedVersion, input.Note }, operationId, cancellationToken);
        ValidateTicket(ticket);
        if (ticket.Id != ticketId || ticket.Status != input.Status || ticket.Version <= input.ExpectedVersion)
            throw new InvalidOperationException("Support transition response is invalid.");
        return ticket;
    }

    public async Task<SupportDiagnostic> AddDiagnosticAsync(Guid organizationId, Guid ticketId,
        AddSupportDiagnosticInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateOrganization(organizationId); ValidateId(ticketId, nameof(ticketId)); ValidateOperation(operationId);
        var digest = input.Sha256.Trim().ToLowerInvariant();
        if (!ValidText(input.Kind, 64) || !ValidText(input.Reference, 512)
            || digest.Length != 64 || digest.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Support diagnostic input is invalid.");
        var value = await Post<SupportDiagnostic>(
            $"api/v1/organizations/{organizationId:D}/support/tickets/{ticketId:D}/diagnostics",
            new { input.Kind, input.Reference, Sha256 = digest }, operationId, cancellationToken);
        ValidateDiagnostic(value, ticketId);
        if (value.Kind != input.Kind.Trim() || value.Reference != input.Reference.Trim()
            || value.Sha256 != digest)
            throw new InvalidOperationException("Support diagnostic response does not match the request.");
        return value;
    }

    private async Task<T> Get<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The support response is empty.");
    }

    private async Task<T> Post<T>(string path, object body, Guid operationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The support response is empty.");
    }

    private static void ValidateTicket(SupportTicketSummary value)
    {
        if (value.Id == Guid.Empty || value.BranchId == Guid.Empty || !ValidText(value.Subject, 200)
            || !ValidText(value.Description, 8000, true) || !Priorities.Contains(value.Priority)
            || !Statuses.Contains(value.Status) || value.Version < 0 || !ValidText(value.OpenedBySubject, 256)
            || value.CreatedAt.Offset != TimeSpan.Zero || value.UpdatedAt.Offset != TimeSpan.Zero
            || value.UpdatedAt < value.CreatedAt)
            throw new InvalidOperationException("Support ticket response is invalid.");
    }

    private static void ValidateDiagnostic(SupportDiagnostic value, Guid ticketId)
    {
        if (value.Id == Guid.Empty || value.TicketId != ticketId || !ValidText(value.Kind, 64)
            || !ValidText(value.Reference, 512) || value.Sha256.Length != 64
            || value.Sha256.Any(character => !Uri.IsHexDigit(character))
            || !ValidText(value.AddedBySubject, 256) || value.CreatedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Support diagnostic response is invalid.");
    }

    private static bool ValidText(string? value, int maximum, bool multiline = false)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim()) return false;
        return !value.Any(character => char.IsControl(character)
            && (!multiline || character is not ('\r' or '\n' or '\t')));
    }

    private static void ValidateOrganization(Guid organizationId)
    { if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required."); }
    private static void ValidateId(Guid value, string name)
    { if (value == Guid.Empty) throw new ArgumentException("Identifier is required.", name); }
    private static void ValidateOperation(Guid operationId)
    { if (operationId == Guid.Empty) throw new ArgumentException("Operation identifier is required."); }
}
