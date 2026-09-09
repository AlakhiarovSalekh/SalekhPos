using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace SalekhPos.IntegrationTests;

public sealed class DeviceTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    [Fact]
    public async Task RegistrationIsAuthorizedIdempotentAndTenantScoped()
    {
        using var owner = Client("owner");
        var registerOperation = Guid.NewGuid();
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/registers") { Content = JsonContent.Create(new { code = "DEVREG-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Device Test Register" }) };
        registerRequest.Headers.Add("Idempotency-Key", registerOperation.ToString("D"));
        using var registerResponse = await owner.SendAsync(registerRequest); registerResponse.EnsureSuccessStatusCode();
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync()); var registerId = registerBody.RootElement.GetProperty("id").GetGuid();
        var operation = Guid.NewGuid(); var code = "POS-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        using var created = await Register(owner, operation, registerId, code, "Front Desktop");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()); var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("desktop", body.RootElement.GetProperty("platform").GetString()); Assert.Equal(1, body.RootElement.GetProperty("syncProtocolVersion").GetInt32()); Assert.Equal("owner", body.RootElement.GetProperty("registeredBy").GetString());
        using var replay = await Register(owner, operation, registerId, code, "Front Desktop"); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var changed = await Register(owner, operation, registerId, code, "Changed Desktop"); Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var detail = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}"); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var list = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices?pageSize=100"); Assert.Equal(HttpStatusCode.OK, list.StatusCode); using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync()); Assert.Contains(listBody.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        using var wrongBranch = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA2}/devices/{id:D}"); Assert.Equal(HttpStatusCode.NotFound, wrongBranch.StatusCode);
        using var denied = await Client("alice").GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalidProtocol = await Register(owner, Guid.NewGuid(), registerId, "BAD-" + code, "Bad Protocol", 2); Assert.Equal(HttpStatusCode.BadRequest, invalidProtocol.StatusCode);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.registered_devices WHERE organization_id=$1 AND device_id=$2", fixture.OrganizationA, id));
        var messageId = Guid.NewGuid();
        using var accepted = await Sync(owner, id, messageId, 1, "{\"saleId\":\"local-1\"}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedBody = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.False(acceptedBody.RootElement.GetProperty("replay").GetBoolean());
        Assert.Equal(64, acceptedBody.RootElement.GetProperty("payloadDigest").GetString()!.Length);
        using var replayed = await Sync(owner, id, messageId, 1, "{\"saleId\":\"local-1\"}");
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        using var replayedBody = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync());
        Assert.True(replayedBody.RootElement.GetProperty("replay").GetBoolean());
        using var changedMessage = await Sync(owner, id, messageId, 1, "{\"saleId\":\"changed\"}");
        Assert.Equal(HttpStatusCode.Conflict, changedMessage.StatusCode);
        using var gap = await Sync(owner, id, Guid.NewGuid(), 3, "{\"saleId\":\"gap\"}");
        Assert.Equal(HttpStatusCode.Conflict, gap.StatusCode);
        using var malformed = await Sync(owner, id, Guid.NewGuid(), 2, "not-json");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var otherOperator = await Sync(Client("manager"), id, Guid.NewGuid(), 2, "{\"saleId\":\"foreign-device\"}");
        Assert.Equal(HttpStatusCode.Conflict, otherOperator.StatusCode);
        using var message = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages/{messageId:D}");
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        using var history = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode); using var historyBody = JsonDocument.Parse(await history.Content.ReadAsStringAsync()); Assert.Single(historyBody.RootElement.GetProperty("items").EnumerateArray());
        using var invalidPage = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages?pageSize=0"); Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using var foreignHistory = await Client("manager").GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages"); Assert.Empty((await foreignHistory.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
        using var checkpoint = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/checkpoint");
        Assert.Equal(HttpStatusCode.OK, checkpoint.StatusCode);
        using var checkpointBody = JsonDocument.Parse(await checkpoint.Content.ReadAsStringAsync());
        Assert.Equal(1, checkpointBody.RootElement.GetProperty("lastAcceptedSequence").GetInt64());
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sync.ingested_messages WHERE organization_id=$1 AND device_id=$2", fixture.OrganizationA, id));
    }
    private async Task<HttpResponseMessage> Register(HttpClient client, Guid operation, Guid registerId, string code, string name, int protocol = 1)
    { using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices") { Content = JsonContent.Create(new { registerId, code, name, platform = "desktop", syncProtocolVersion = protocol }) }; request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request); }
    private HttpClient Client(string subject) { var c = fixture.Factory.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject)); return c; }
    private Task<HttpResponseMessage> Sync(HttpClient client, Guid deviceId, Guid messageId, long sequence, string payload) =>
        client.PostAsJsonAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{deviceId:D}/sync/messages",
            new { messageId, sequence, protocolVersion = 1, messageType = "sale.completed.v1", payload });
}
