using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SupportTests(AccessFixture fixture)
{
    [Fact]
    public async Task SupportMutationsReplayByOperationIdAndRejectChangedIntent()
    {
        await fixture.GrantAsync("owner", fixture.OrganizationA, "support.create");
        await fixture.GrantAsync("owner", fixture.OrganizationA, "support.view");
        await fixture.GrantAsync("owner", fixture.OrganizationA, "support.manage");

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        var root = $"/api/v1/organizations/{fixture.OrganizationA:D}/support/tickets";

        var createOperation = Guid.NewGuid();
        using var firstCreate = await Post(client, root, createOperation, new
        {
            BranchId = fixture.BranchA,
            Subject = "Receipt printer unavailable",
            Description = "The front register cannot print receipts.",
            Priority = "high"
        });
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        using var firstTicket = JsonDocument.Parse(await firstCreate.Content.ReadAsStringAsync());
        var ticketId = firstTicket.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1, firstTicket.RootElement.GetProperty("version").GetInt32());

        using var replayCreate = await Post(client, root, createOperation, new
        {
            BranchId = fixture.BranchA,
            Subject = "Receipt printer unavailable",
            Description = "The front register cannot print receipts.",
            Priority = "high"
        });
        Assert.Equal(HttpStatusCode.OK, replayCreate.StatusCode);
        using var replayedTicket = JsonDocument.Parse(await replayCreate.Content.ReadAsStringAsync());
        Assert.Equal(ticketId, replayedTicket.RootElement.GetProperty("id").GetGuid());

        using var changedCreate = await Post(client, root, createOperation, new
        {
            BranchId = fixture.BranchA,
            Subject = "Changed request",
            Description = "The front register cannot print receipts.",
            Priority = "high"
        });
        Assert.Equal(HttpStatusCode.Conflict, changedCreate.StatusCode);

        var transitionOperation = Guid.NewGuid();
        var transitionPath = $"{root}/{ticketId:D}/transitions";
        using var firstTransition = await Post(client, transitionPath, transitionOperation, new
        {
            Status = "in_progress",
            ExpectedVersion = 1,
            Note = "Investigation started."
        });
        Assert.Equal(HttpStatusCode.OK, firstTransition.StatusCode);
        using var transitioned = JsonDocument.Parse(await firstTransition.Content.ReadAsStringAsync());
        Assert.Equal(2, transitioned.RootElement.GetProperty("version").GetInt32());

        using var replayTransition = await Post(client, transitionPath, transitionOperation, new
        {
            Status = "in_progress",
            ExpectedVersion = 1,
            Note = "Investigation started."
        });
        Assert.Equal(HttpStatusCode.OK, replayTransition.StatusCode);
        using var replayedTransition = JsonDocument.Parse(await replayTransition.Content.ReadAsStringAsync());
        Assert.Equal(2, replayedTransition.RootElement.GetProperty("version").GetInt32());

        using var changedTransition = await Post(client, transitionPath, transitionOperation, new
        {
            Status = "in_progress",
            ExpectedVersion = 1,
            Note = "Changed transition intent."
        });
        Assert.Equal(HttpStatusCode.Conflict, changedTransition.StatusCode);

        var diagnosticOperation = Guid.NewGuid();
        var diagnosticPath = $"{root}/{ticketId:D}/diagnostics";
        var digest = new string('a', 64);
        using var firstDiagnostic = await Post(client, diagnosticPath, diagnosticOperation, new
        {
            Kind = "log",
            Reference = "object://support/diagnostic-1",
            Sha256 = digest
        });
        Assert.Equal(HttpStatusCode.Created, firstDiagnostic.StatusCode);
        using var diagnostic = JsonDocument.Parse(await firstDiagnostic.Content.ReadAsStringAsync());
        var diagnosticId = diagnostic.RootElement.GetProperty("id").GetGuid();

        using var replayDiagnostic = await Post(client, diagnosticPath, diagnosticOperation, new
        {
            Kind = "log",
            Reference = "object://support/diagnostic-1",
            Sha256 = digest
        });
        Assert.Equal(HttpStatusCode.OK, replayDiagnostic.StatusCode);
        using var replayedDiagnostic = JsonDocument.Parse(await replayDiagnostic.Content.ReadAsStringAsync());
        Assert.Equal(diagnosticId, replayedDiagnostic.RootElement.GetProperty("id").GetGuid());

        using var changedDiagnostic = await Post(client, diagnosticPath, diagnosticOperation, new
        {
            Kind = "log",
            Reference = "object://support/changed",
            Sha256 = digest
        });
        Assert.Equal(HttpStatusCode.Conflict, changedDiagnostic.StatusCode);

        using var duplicateEvidence = await Post(client, diagnosticPath, Guid.NewGuid(), new
        {
            Kind = "log",
            Reference = "object://support/diagnostic-1",
            Sha256 = digest
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateEvidence.StatusCode);

        using var list = await client.GetAsync($"{root}?pageSize=50&status=in_progress");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(page.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == ticketId);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Guid operationId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request);
    }
}
