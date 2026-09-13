using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SalekhPos.Devices.Application.Devices;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class ShiftDeviceProofTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task StrongShiftProofIsRegisterBoundReplaySafeAndIndependentOfHumanAuthorization()
    {
        using var owner = Client("owner");
        var registerA = await CreateRegister(owner, "Strong proof A");
        var registerB = await CreateRegister(owner, "Strong proof B");
        using var device = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerA);

        var operationId = Guid.NewGuid();
        using var created = await device.OpenShiftAsync(fixture, owner, registerA, operationId, 12.34m);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var replayNonce = RandomNumberGenerator.GetBytes(32);
        var replayTime = DateTimeOffset.UtcNow;
        var replayOperation = Guid.NewGuid();
        var replayProof = new ShiftProofOverrides(Timestamp: replayTime, Nonce: replayNonce);
        using var first = await device.OpenShiftAsync(fixture, owner, registerB, replayOperation, overrides: replayProof);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode); // register binding is checked before nonce acceptance
        using var wrongRegister = await device.OpenShiftAsync(fixture, owner, registerB, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongRegister.StatusCode);

        var registerC = await CreateRegister(owner, "Strong proof replay");
        using var replayDevice = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerC);
        var acceptedOperation = Guid.NewGuid();
        var acceptedProof = new ShiftProofOverrides(Timestamp: replayTime, Nonce: replayNonce);
        using var accepted = await replayDevice.OpenShiftAsync(fixture, owner, registerC, acceptedOperation, overrides: acceptedProof);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var replayed = await replayDevice.OpenShiftAsync(fixture, owner, registerC, acceptedOperation, overrides: acceptedProof);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);

        var cashier = "shared-cashier-" + Guid.NewGuid().ToString("N");
        await fixture.AddMembershipAsync(cashier, fixture.OrganizationA, "branch", fixture.BusinessA, branch: fixture.BranchA);
        await fixture.GrantAsync(cashier, fixture.OrganizationA, "shifts.open");
        var registerD = await CreateRegister(owner, "Shared cashier terminal");
        using var sharedDevice = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerD);
        using var cashierClient = Client(cashier);
        using var shared = await sharedDevice.OpenShiftAsync(fixture, cashierClient, registerD, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, shared.StatusCode);
        Assert.Equal(cashier, (await shared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("openedBy").GetString());

        var registerE = await CreateRegister(owner, "Unauthorized human");
        using var unauthorizedDevice = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerE);
        using var unauthorizedClient = Client("alice");
        using var denied = await unauthorizedDevice.OpenShiftAsync(fixture, unauthorizedClient, registerE, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task MissingWrongAndCrossDeviceProofsAreRejectedWithoutCreatingShifts()
    {
        using var owner = Client("owner");
        var registerId = await CreateRegister(owner, "Rejected proofs");
        using var deviceA = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerId);
        using var deviceB = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerId);

        using var missing = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/shifts/open")
        { Content = JsonContent.Create(new { registerId, currency = "GEL", openingBalance = 0m }) };
        missing.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var missingResponse = await owner.SendAsync(missing);
        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);

        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var badSignature = await deviceA.OpenShiftAsync(fixture, owner, registerId, Guid.NewGuid(),
            overrides: new(SigningKey: wrongKey));
        Assert.Equal(HttpStatusCode.Unauthorized, badSignature.StatusCode);

        using var substitutedDevice = await deviceA.OpenShiftAsync(fixture, owner, registerId, Guid.NewGuid(),
            overrides: new(HeaderDeviceId: deviceB.DeviceId, SignedDeviceId: deviceA.DeviceId));
        Assert.Equal(HttpStatusCode.Unauthorized, substitutedDevice.StatusCode);
        Assert.Equal(0L, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND register_id=$3",
            fixture.OrganizationA, fixture.BranchA, registerId));
    }

    [Fact]
    public async Task BodyOperationAndPathTamperingInvalidateShiftProof()
    {
        using var owner = Client("owner");
        var registerId = await CreateRegister(owner, "Tamper proof");
        using var device = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, registerId);

        async Task Reject(byte[] body, ShiftProofOverrides overrides)
        {
            using var response = await device.OpenShiftRawAsync(fixture, owner, Guid.NewGuid(), body, overrides);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        byte[] Body(Guid id, string currency, decimal balance) =>
            JsonSerializer.SerializeToUtf8Bytes(new { registerId = id, currency, openingBalance = balance }, WebJson);
        var validBody = Body(registerId, "GEL", 4m);
        await Reject(Body(registerId, "USD", 4m), new(SignedBody: validBody));
        await Reject(Body(registerId, "GEL", 5m), new(SignedBody: validBody));
        await Reject(Body(Guid.NewGuid(), "GEL", 4m), new(SignedBody: validBody));
        await Reject(validBody, new(SignedOperationIdentity: $"shift-open:{Guid.NewGuid():D}"));
        await Reject(validBody, new(SignedPath: $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/shifts/closed"));
        Assert.Equal(0L, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND register_id=$3",
            fixture.OrganizationA, fixture.BranchA, registerId));
    }

    [Fact]
    public async Task PendingAndRevokedCredentialsCannotOpenShifts()
    {
        using var owner = Client("owner");
        var pendingRegister = await CreateRegister(owner, "Pending proof");
        using var pending = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, pendingRegister, trust: false);
        using var pendingResponse = await pending.OpenShiftAsync(fixture, owner, pendingRegister, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, pendingResponse.StatusCode);

        var revokedRegister = await CreateRegister(owner, "Revoked proof");
        using var revoked = await TrustedDeviceTestClient.EnrollAsync(fixture, owner, revokedRegister);
        await revoked.RevokeAsync(fixture, owner);
        using var revokedResponse = await revoked.OpenShiftAsync(fixture, owner, revokedRegister, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
    }

    [Fact]
    public async Task LegacyCredentiallessVerifierBehaviorRemainsOptional()
    {
        using var owner = Client("owner");
        var registerId = await CreateRegister(owner, "Legacy sync compatibility");
        var deviceId = Guid.NewGuid();
        await fixture.ExecuteAsync("INSERT INTO devices.registered_devices(organization_id,device_id,operation_id,branch_id,register_id,code,name,platform,status,sync_protocol_version,registered_at,issuer,registered_by) VALUES($1,$2,$3,$4,$5,$6,'Legacy terminal','desktop','active',1,now(),$7,$8)",
            fixture.OrganizationA, deviceId, Guid.NewGuid(), fixture.BranchA, registerId,
            "LEGACY-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), AccessFixture.Issuer, "owner");
        var verifier = fixture.Factory.Services.GetRequiredService<IDeviceRequestProofVerifier>();
        var context = new DeviceRequestProofContext(new(AccessFixture.Issuer, "owner"), fixture.OrganizationA,
            fixture.BranchA, deviceId, "POST", "/legacy", "legacy", new string('0', 64), new(null, null, null, null));
        await verifier.VerifyAsync(context, CancellationToken.None);
        await Assert.ThrowsAsync<DeviceRequestAuthenticationException>(() => verifier.VerifyAsync(
            context with { RequireCredential = true, ExpectedRegisterId = registerId }, CancellationToken.None));
    }

    private async Task<Guid> CreateRegister(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/registers")
        { Content = JsonContent.Create(new { code = "PROOF-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), name }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private HttpClient Client(string subject)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(subject));
        return client;
    }
}
