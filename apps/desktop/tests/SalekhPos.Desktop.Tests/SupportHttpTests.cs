using System.Net;
using System.Text;
using SalekhPos.Desktop.Application.Management;
using SalekhPos.Desktop.Infrastructure.Management;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class SupportHttpTests
{
    [Fact]
    public async Task ListsScopedSupportTicketsAndValidatesWorkflowValues()
    {
        var organization = Guid.NewGuid(); var ticket = Guid.NewGuid(); var branch = Guid.NewGuid();
        var captured = "";
        using var client = Client(request =>
        {
            captured = request.RequestUri!.PathAndQuery;
            return Json($$"""{"items":[{"id":"{{ticket:D}}","branchId":"{{branch:D}}","subject":"Printer","description":"Offline","priority":"high","status":"open","version":0,"openedBySubject":"operator","createdAt":"2026-09-18T08:00:00Z","updatedAt":"2026-09-18T08:00:00Z"}],"nextCursor":null}""");
        });
        var page = await new HttpSupportManager(client).ListAsync(organization, 50, null, "open", default);
        Assert.Single(page.Items);
        Assert.Equal($"/api/v1/organizations/{organization:D}/support/tickets?pageSize=50&status=open", captured);

        using var invalid = Client(_ => Json($$"""{"items":[{"id":"{{ticket:D}}","branchId":"{{branch:D}}","subject":"Printer","description":"Offline","priority":"blocker","status":"open","version":0,"openedBySubject":"operator","createdAt":"2026-09-18T08:00:00Z","updatedAt":"2026-09-18T08:00:00Z"}],"nextCursor":null}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HttpSupportManager(invalid).ListAsync(organization, 50, null, null, default));
    }

    [Fact]
    public async Task CreateUsesStableCallerOperationAndValidatesReturnedScope()
    {
        var organization = Guid.NewGuid(); var ticket = Guid.NewGuid(); var branch = Guid.NewGuid(); var operation = Guid.NewGuid();
        string? header = null;
        using var client = Client(request =>
        {
            header = request.Headers.GetValues("Idempotency-Key").Single();
            return Json($$"""{"id":"{{ticket:D}}","branchId":"{{branch:D}}","subject":"Printer","description":"Offline","priority":"normal","status":"open","version":0,"openedBySubject":"operator","createdAt":"2026-09-18T08:00:00Z","updatedAt":"2026-09-18T08:00:00Z"}""");
        });
        var result = await new HttpSupportManager(client).CreateAsync(organization,
            new(branch, "Printer", "Offline", "normal"), operation, default);
        Assert.Equal(ticket, result.Id); Assert.Equal(operation.ToString("D"), header);
    }

    [Fact]
    public async Task DetailRejectsCrossTicketDiagnostics()
    {
        var organization = Guid.NewGuid(); var ticket = Guid.NewGuid(); var other = Guid.NewGuid();
        using var client = Client(_ => Json($$"""{"ticket":{"id":"{{ticket:D}}","branchId":null,"subject":"Issue","description":"Details","priority":"low","status":"open","version":0,"openedBySubject":"operator","createdAt":"2026-09-18T08:00:00Z","updatedAt":"2026-09-18T08:00:00Z"},"diagnostics":[{"id":"{{Guid.NewGuid():D}}","ticketId":"{{other:D}}","kind":"log","reference":"object://1","sha256":"{{new string('a',64)}}","addedBySubject":"manager","createdAt":"2026-09-18T08:10:00Z"}]}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HttpSupportManager(client).ReadAsync(organization, ticket, default));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new Handler(response)) { BaseAddress = new("https://pos.test/") };
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
