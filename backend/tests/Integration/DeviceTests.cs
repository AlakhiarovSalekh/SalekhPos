using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
        var operation = Guid.NewGuid(); var code = "POS-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(); using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var created = await Register(owner, operation, registerId, code, "Front Desktop", key: deviceKey);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()); var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("desktop", body.RootElement.GetProperty("platform").GetString()); Assert.Equal("pending", body.RootElement.GetProperty("status").GetString()); Assert.Equal(1, body.RootElement.GetProperty("syncProtocolVersion").GetInt32()); Assert.Equal("owner", body.RootElement.GetProperty("registeredBy").GetString());
        var credential = body.RootElement.GetProperty("credential"); var credentialId = credential.GetProperty("id").GetGuid(); var challenge = credential.GetProperty("proofChallenge").GetString()!; Assert.Equal("ecdsa-p256-sha256", credential.GetProperty("algorithm").GetString());
        Assert.False(body.RootElement.TryGetProperty("publicKey", out _)); Assert.False(credential.TryGetProperty("publicKey", out _)); Assert.False(credential.TryGetProperty("fingerprint", out _));
        using var replay = await Register(owner, operation, registerId, code, "Front Desktop", key: deviceKey); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var changed = await Register(owner, operation, registerId, code, "Changed Desktop", key: deviceKey); Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var changedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var changedCredential = await Register(owner, operation, registerId, code, "Front Desktop", key: changedKey); Assert.Equal(HttpStatusCode.Conflict, changedCredential.StatusCode);
        var duplicateKeyOperation = Guid.NewGuid(); using var duplicateKey = await Register(owner, duplicateKeyOperation, registerId, "DUP-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), "Duplicate Key", key: deviceKey); Assert.Equal(HttpStatusCode.Conflict, duplicateKey.StatusCode);
        using var unsupportedCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384); using var unsupportedKey = await Register(owner, Guid.NewGuid(), registerId, "P384-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), "Unsupported Curve", key: unsupportedCurve); Assert.Equal(HttpStatusCode.BadRequest, unsupportedKey.StatusCode);
        using var detail = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{id:D}"); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var list = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices?pageSize=100"); Assert.Equal(HttpStatusCode.OK, list.StatusCode); using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync()); Assert.Contains(listBody.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        using var wrongBranch = await owner.GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA2}/devices/{id:D}"); Assert.Equal(HttpStatusCode.NotFound, wrongBranch.StatusCode);
        using var denied = await Client("alice").GetAsync($"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalidProtocol = await Register(owner, Guid.NewGuid(), registerId, "BAD-" + code, "Bad Protocol", 2); Assert.Equal(HttpStatusCode.BadRequest, invalidProtocol.StatusCode);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.registered_devices WHERE organization_id=$1 AND device_id=$2", fixture.OrganizationA, id));
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.device_credentials WHERE organization_id=$1 AND device_id=$2 AND status='pending'", fixture.OrganizationA, id));
        Assert.Equal(32, await fixture.ScalarAsync<int>("SELECT octet_length(public_key_fingerprint) FROM devices.device_credentials WHERE organization_id=$1 AND device_id=$2", fixture.OrganizationA, id));
        Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.registered_devices WHERE organization_id=$1 AND operation_id=$2", fixture.OrganizationA, duplicateKeyOperation));
        using var pendingSync = await Sync(owner, id, Guid.NewGuid(), 1, OfflinePayload()); Assert.Equal(HttpStatusCode.Conflict, pendingSync.StatusCode);
        using var deniedTrust = await Trust(Client("alice"), id, credentialId, challenge, deviceKey, Guid.NewGuid()); Assert.Equal(HttpStatusCode.Forbidden, deniedTrust.StatusCode);
        var operationBoundHeader = Guid.NewGuid(); using var operationBoundProof = await Trust(owner, id, credentialId, challenge, deviceKey, operationBoundHeader, proofOperation: Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, operationBoundProof.StatusCode);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.device_credentials WHERE organization_id=$1 AND device_id=$2 AND status='pending'", fixture.OrganizationA, id));
        using var otherFingerprintKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var fingerprintBoundProof = await Trust(owner, id, credentialId, challenge, deviceKey, Guid.NewGuid(), fingerprintKey: otherFingerprintKey); Assert.Equal(HttpStatusCode.BadRequest, fingerprintBoundProof.StatusCode);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var invalidProof = await Trust(owner, id, credentialId, challenge, wrongKey, Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, invalidProof.StatusCode);
        using var malformedSignature = await TrustWithSignature(owner, id, credentialId, challenge, "not-base64", Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, malformedSignature.StatusCode);
        using var shortSignature = await TrustWithSignature(owner, id, credentialId, challenge, Convert.ToBase64String(RandomNumberGenerator.GetBytes(63)), Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, shortSignature.StatusCode);
        using var oversizedSignature = await TrustWithSignature(owner, id, credentialId, challenge, Convert.ToBase64String(RandomNumberGenerator.GetBytes(65)), Guid.NewGuid()); Assert.Equal(HttpStatusCode.BadRequest, oversizedSignature.StatusCode);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.device_credentials WHERE organization_id=$1 AND device_id=$2 AND status='pending'", fixture.OrganizationA, id));
        var trustOperation = Guid.NewGuid(); using var trusted = await Trust(owner, id, credentialId, challenge, deviceKey, trustOperation); Assert.Equal(HttpStatusCode.OK, trusted.StatusCode); using var trustedBody = JsonDocument.Parse(await trusted.Content.ReadAsStringAsync()); Assert.Equal("active", trustedBody.RootElement.GetProperty("status").GetString());
        using var trustReplay = await Trust(owner, id, credentialId, challenge, deviceKey, trustOperation); Assert.Equal(HttpStatusCode.OK, trustReplay.StatusCode);
        using var trustConflict = await Trust(owner, id, credentialId, challenge, deviceKey, Guid.NewGuid()); Assert.Equal(HttpStatusCode.Conflict, trustConflict.StatusCode);
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
        using var deniedRevocation = await Revoke(Client("alice"), id, Guid.NewGuid(), "Unauthorized retirement"); Assert.Equal(HttpStatusCode.Forbidden, deniedRevocation.StatusCode);
        var revokeOperation = Guid.NewGuid(); using var revoked = await Revoke(owner, id, revokeOperation, "Device retired"); Assert.Equal(HttpStatusCode.OK, revoked.StatusCode); using var revokedBody = JsonDocument.Parse(await revoked.Content.ReadAsStringAsync()); Assert.Equal("revoked", revokedBody.RootElement.GetProperty("status").GetString()); Assert.Equal("owner", revokedBody.RootElement.GetProperty("revokedBy").GetString());
        using var revokeReplay = await Revoke(owner, id, revokeOperation, "Device retired"); Assert.Equal(HttpStatusCode.OK, revokeReplay.StatusCode);
        using var revokeConflict = await Revoke(owner, id, revokeOperation, "Different reason"); Assert.Equal(HttpStatusCode.Conflict, revokeConflict.StatusCode);
        using var revokedSync = await Sync(owner, id, Guid.NewGuid(), 5, OfflinePayload()); Assert.Equal(HttpStatusCode.Conflict, revokedSync.StatusCode);
        Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM devices.device_credentials WHERE organization_id=$1 AND device_id=$2 AND status='revoked' AND revocation_operation_id=$3", fixture.OrganizationA, id, revokeOperation));
    }
    [Fact]
    public async Task ConcurrentDevicesCannotOversellTheSameStock()
    {
        using var owner = Client("owner");
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/registers") { Content = JsonContent.Create(new { code = "RACE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Offline Race Register" }) }; registerRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var register = await owner.SendAsync(registerRequest); register.EnsureSuccessStatusCode(); using var registerBody = JsonDocument.Parse(await register.Content.ReadAsStringAsync()); var registerId = registerBody.RootElement.GetProperty("id").GetGuid();
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var firstDevice = await Register(owner, Guid.NewGuid(), registerId, "RACE-A-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Race A", key: firstKey); using var firstBody = JsonDocument.Parse(await firstDevice.Content.ReadAsStringAsync()); var firstId = firstBody.RootElement.GetProperty("id").GetGuid(); var firstCredential = firstBody.RootElement.GetProperty("credential");
        using var secondDevice = await Register(owner, Guid.NewGuid(), registerId, "RACE-B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Race B", key: secondKey); using var secondBody = JsonDocument.Parse(await secondDevice.Content.ReadAsStringAsync()); var secondId = secondBody.RootElement.GetProperty("id").GetGuid(); var secondCredential = secondBody.RootElement.GetProperty("credential");
        using var firstTrust = await Trust(owner, firstId, firstCredential.GetProperty("id").GetGuid(), firstCredential.GetProperty("proofChallenge").GetString()!, firstKey, Guid.NewGuid()); firstTrust.EnsureSuccessStatusCode();
        using var secondTrust = await Trust(owner, secondId, secondCredential.GetProperty("id").GetGuid(), secondCredential.GetProperty("proofChallenge").GetString()!, secondKey, Guid.NewGuid()); secondTrust.EnsureSuccessStatusCode();
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
    private async Task<HttpResponseMessage> Register(HttpClient client, Guid operation, Guid registerId, string code, string name, int protocol = 1, ECDsa? key = null)
    { using var generated = key is null ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : null; var publicKey = Convert.ToBase64String((key ?? generated!).ExportSubjectPublicKeyInfo()); using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices") { Content = JsonContent.Create(new { registerId, code, name, platform = "desktop", syncProtocolVersion = protocol, publicKey }) }; request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request); }
    private Task<HttpResponseMessage> Trust(HttpClient client, Guid deviceId, Guid credentialId, string challenge, ECDsa key, Guid operation, Guid? proofOperation = null, ECDsa? fingerprintKey = null)
    { var fingerprint = SHA256.HashData((fingerprintKey ?? key).ExportSubjectPublicKeyInfo()); var proof = Encoding.UTF8.GetBytes($"salekhpos-device-trust-v1\n{fixture.OrganizationA:D}\n{fixture.BranchA:D}\n{deviceId:D}\n{credentialId:D}\n{(proofOperation ?? operation):D}\n{challenge}\n{Convert.ToBase64String(fingerprint)}"); var signature = Convert.ToBase64String(key.SignData(proof, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)); return TrustWithSignature(client, deviceId, credentialId, challenge, signature, operation); }
    private async Task<HttpResponseMessage> TrustWithSignature(HttpClient client, Guid deviceId, Guid credentialId, string challenge, string signature, Guid operation)
    { using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{deviceId:D}/trust") { Content = JsonContent.Create(new { credentialId, proofChallenge = challenge, signature }) }; request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request); }
    private async Task<HttpResponseMessage> Revoke(HttpClient client, Guid deviceId, Guid operation, string reason)
    { using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{fixture.OrganizationA}/branches/{fixture.BranchA}/devices/{deviceId:D}/revoke") { Content = JsonContent.Create(new { reason }) }; request.Headers.Add("Idempotency-Key", operation.ToString("D")); return await client.SendAsync(request); }
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
