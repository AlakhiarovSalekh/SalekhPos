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
        var rejectedSaleId = Guid.NewGuid(); var payload = OfflinePayload(saleId: rejectedSaleId);
        using var accepted = await Sync(owner, id, messageId, 1, payload);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedBody = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.False(acceptedBody.RootElement.GetProperty("replay").GetBoolean());
        Assert.Equal(rejectedSaleId, acceptedBody.RootElement.GetProperty("saleId").GetGuid());
        Assert.Equal("rejected", acceptedBody.RootElement.GetProperty("status").GetString());
        Assert.Equal("shift_conflict", acceptedBody.RootElement.GetProperty("resultCode").GetString());
        Assert.Equal(64, acceptedBody.RootElement.GetProperty("payloadDigest").GetString()!.Length);
        Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sales.completed_sales WHERE organization_id=$1 AND sale_id=$2", fixture.OrganizationA, rejectedSaleId));
        using var replayed = await Sync(owner, id, messageId, 1, payload);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        using var replayedBody = JsonDocument.Parse(await replayed.Content.ReadAsStringAsync());
        Assert.True(replayedBody.RootElement.GetProperty("replay").GetBoolean());
        using var changedMessage = await Sync(owner, id, messageId, 1, OfflinePayload());
        Assert.Equal(HttpStatusCode.Conflict, changedMessage.StatusCode);
        using var gap = await Sync(owner, id, Guid.NewGuid(), 3, OfflinePayload());
        Assert.Equal(HttpStatusCode.Conflict, gap.StatusCode);
        using var malformed = await Sync(owner, id, Guid.NewGuid(), 2, "not-json");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var invalidFinancials = await Sync(owner, id, Guid.NewGuid(), 2, OfflinePayload(grandTotal: 10m));
        Assert.Equal(HttpStatusCode.BadRequest, invalidFinancials.StatusCode);
        using var otherOperator = await Sync(Client("manager"), id, Guid.NewGuid(), 2, OfflinePayload());
        Assert.Equal(HttpStatusCode.Conflict, otherOperator.StatusCode);
        using var message = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages/{messageId:D}");
        Assert.Equal(HttpStatusCode.OK, message.StatusCode);
        using var history = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode); using var historyBody = JsonDocument.Parse(await history.Content.ReadAsStringAsync()); Assert.Single(historyBody.RootElement.GetProperty("items").EnumerateArray());
        using var invalidPage = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages?pageSize=0"); Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using var foreignHistory = await Client("manager").GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/messages"); Assert.Empty((await foreignHistory.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
        var (productId, priceId) = await PrepareProduct(owner);
        using var shift = await OpenShift(owner, registerId); shift.EnsureSuccessStatusCode();
        using var shiftBody = JsonDocument.Parse(await shift.Content.ReadAsStringAsync()); var shiftId = shiftBody.RootElement.GetProperty("id").GetGuid();
        var saleId = Guid.NewGuid(); var appliedPayload = OfflinePayload(saleId: saleId, shiftId: shiftId, registerId: registerId, productId: productId, priceId: priceId);
        var appliedMessageId = Guid.NewGuid(); using var applied = await Sync(owner, id, appliedMessageId, 2, appliedPayload);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode); using var appliedBody = JsonDocument.Parse(await applied.Content.ReadAsStringAsync()); Assert.Equal("applied", appliedBody.RootElement.GetProperty("status").GetString());
        using var appliedReplay = await Sync(owner, id, appliedMessageId, 2, appliedPayload); using var appliedReplayBody = JsonDocument.Parse(await appliedReplay.Content.ReadAsStringAsync()); Assert.True(appliedReplayBody.RootElement.GetProperty("replay").GetBoolean());
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sales.completed_sales WHERE organization_id=$1 AND sale_id=$2", fixture.OrganizationA, saleId));
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM payments.payment_records WHERE organization_id=$1 AND sale_id=$2", fixture.OrganizationA, saleId));
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM inventory.stock_movements WHERE organization_id=$1 AND kind='sale' AND reason='Synchronized offline cash sale' AND product_id=$2", fixture.OrganizationA, productId));
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sales.outbox_messages WHERE organization_id=$1 AND sale_id=$2", fixture.OrganizationA, saleId));
        var priceConflictSaleId = Guid.NewGuid(); var priceConflictPayload = OfflinePayload(saleId: priceConflictSaleId, shiftId: shiftId, registerId: registerId, productId: productId, priceId: Guid.NewGuid());
        var priceConflictMessageId = Guid.NewGuid(); using var priceConflict = await Sync(owner, id, priceConflictMessageId, 3, priceConflictPayload);
        using var priceConflictBody = JsonDocument.Parse(await priceConflict.Content.ReadAsStringAsync()); Assert.Equal("price_conflict", priceConflictBody.RootElement.GetProperty("resultCode").GetString());
        using var priceConflictReplay = await Sync(owner, id, priceConflictMessageId, 3, priceConflictPayload); using var priceConflictReplayBody = JsonDocument.Parse(await priceConflictReplay.Content.ReadAsStringAsync()); Assert.True(priceConflictReplayBody.RootElement.GetProperty("replay").GetBoolean());
        using var duplicateSale = await Sync(owner, id, Guid.NewGuid(), 4, OfflinePayload(saleId: saleId, shiftId: shiftId, registerId: registerId, productId: productId, priceId: priceId));
        using var duplicateSaleBody = JsonDocument.Parse(await duplicateSale.Content.ReadAsStringAsync()); Assert.Equal("sale_conflict", duplicateSaleBody.RootElement.GetProperty("resultCode").GetString());
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sales.completed_sales WHERE organization_id=$1 AND sale_id=$2", fixture.OrganizationA, saleId));
        using var checkpoint = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}/sync/checkpoint");
        Assert.Equal(HttpStatusCode.OK, checkpoint.StatusCode);
        using var checkpointBody = JsonDocument.Parse(await checkpoint.Content.ReadAsStringAsync());
        Assert.Equal(4, checkpointBody.RootElement.GetProperty("lastAcceptedSequence").GetInt64());
        Assert.Equal(4L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sync.ingested_messages WHERE organization_id=$1 AND device_id=$2", fixture.OrganizationA, id));
        Assert.Equal(4L, await fixture.ScalarAsync<long>("SELECT count(*) FROM sync.message_results WHERE organization_id=$1", fixture.OrganizationA));
    }
    [Fact]
    public async Task ConcurrentDevicesCannotOversellTheSameStock()
    {
        using var owner = Client("owner");
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/registers") { Content = JsonContent.Create(new { code = "RACE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Offline Race Register" }) }; registerRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var register = await owner.SendAsync(registerRequest); register.EnsureSuccessStatusCode(); using var registerBody = JsonDocument.Parse(await register.Content.ReadAsStringAsync()); var registerId = registerBody.RootElement.GetProperty("id").GetGuid();
        using var firstDevice = await Register(owner, Guid.NewGuid(), registerId, "RACE-A-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Race A"); using var firstBody = JsonDocument.Parse(await firstDevice.Content.ReadAsStringAsync()); var firstId = firstBody.RootElement.GetProperty("id").GetGuid();
        using var secondDevice = await Register(owner, Guid.NewGuid(), registerId, "RACE-B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Race B"); using var secondBody = JsonDocument.Parse(await secondDevice.Content.ReadAsStringAsync()); var secondId = secondBody.RootElement.GetProperty("id").GetGuid();
        using var shift = await OpenShift(owner, registerId); shift.EnsureSuccessStatusCode(); using var shiftBody = JsonDocument.Parse(await shift.Content.ReadAsStringAsync()); var shiftId = shiftBody.RootElement.GetProperty("id").GetGuid();
        var (productId, priceId) = await PrepareProduct(owner, 1m);
        var responses = await Task.WhenAll(
            Sync(owner, firstId, Guid.NewGuid(), 1, OfflinePayload(shiftId: shiftId, registerId: registerId, productId: productId, priceId: priceId)),
            Sync(owner, secondId, Guid.NewGuid(), 1, OfflinePayload(shiftId: shiftId, registerId: registerId, productId: productId, priceId: priceId)));
        try
        {
            var codes = new List<string>(); foreach (var response in responses) { response.EnsureSuccessStatusCode(); using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); codes.Add(body.RootElement.GetProperty("resultCode").GetString()!); }
            Assert.Equal(["applied", "insufficient_stock"], [.. codes.Order()]);
            Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM inventory.stock_movements WHERE organization_id=$1 AND branch_id=$2 AND product_id=$3 AND kind='sale'", fixture.OrganizationA, fixture.BranchA, productId));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    private async Task<HttpResponseMessage> Register(HttpClient client, Guid operation, Guid registerId, string code, string name, int protocol = 1)
    { using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices") { Content = JsonContent.Create(new { registerId, code, name, platform = "desktop", syncProtocolVersion = protocol }) }; request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request); }
    private HttpClient Client(string subject) { var c = fixture.Factory.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject)); return c; }
    private Task<HttpResponseMessage> Sync(HttpClient client, Guid deviceId, Guid messageId, long sequence, string payload) =>
        client.PostAsJsonAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{deviceId:D}/sync/messages",
            new { messageId, sequence, protocolVersion = 1, messageType = "sale.completed.v1", payload });
    private async Task<(Guid ProductId, Guid PriceId)> PrepareProduct(HttpClient owner, decimal stockQuantity = 5m)
    {
        using var productRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/products") { Content = JsonContent.Create(new { sku = "SYNC." + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = "Offline Sync Item", unitCode = "EA", barcode = (string?)null }) }; productRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var product = await owner.SendAsync(productRequest); product.EnsureSuccessStatusCode(); using var productBody = JsonDocument.Parse(await product.Content.ReadAsStringAsync()); var productId = productBody.RootElement.GetProperty("id").GetGuid();
        using var priceRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/pricing/prices") { Content = JsonContent.Create(new { productId, branchId = fixture.BranchA, amount = 2m, currency = "GEL", taxMode = "inclusive", taxRate = 0m, validFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), validUntil = (DateTimeOffset?)null }) }; priceRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var price = await owner.SendAsync(priceRequest); price.EnsureSuccessStatusCode(); using var priceBody = JsonDocument.Parse(await price.Content.ReadAsStringAsync()); var priceId = priceBody.RootElement.GetProperty("id").GetGuid();
        using var stockRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/inventory/movements") { Content = JsonContent.Create(new { productId, kind = "receipt", quantity = stockQuantity, reason = "Offline sync test stock", occurredAt = DateTimeOffset.UtcNow }) }; stockRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); using var stock = await owner.SendAsync(stockRequest); stock.EnsureSuccessStatusCode(); return (productId, priceId);
    }
    private async Task<HttpResponseMessage> OpenShift(HttpClient owner, Guid registerId) { using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/shifts/open") { Content = JsonContent.Create(new { registerId, currency = "GEL", openingBalance = 0m }) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); return await owner.SendAsync(request); }
    private static string OfflinePayload(decimal grandTotal = 2m, Guid? saleId = null, Guid? shiftId = null,
        Guid? registerId = null, Guid? productId = null, Guid? priceId = null) => JsonSerializer.Serialize(new
        {
            saleId = saleId ?? Guid.NewGuid(),
            shiftId = shiftId ?? Guid.NewGuid(),
            registerId = registerId ?? Guid.NewGuid(),
            completedAt = DateTimeOffset.UtcNow,
            currency = "GEL",
            cashReceived = 3m,
            netTotal = 2m,
            taxTotal = 0m,
            grandTotal,
            changeDue = 1m,
            lines = new[] { new { lineNumber = 1, productId = productId ?? Guid.NewGuid(), priceId = priceId ?? Guid.NewGuid(), quantity = 1m,
            unitAmount = 2m, taxMode = "inclusive", taxRate = 0m, netAmount = 2m, taxAmount = 0m, grossAmount = 2m } }
        });
}
