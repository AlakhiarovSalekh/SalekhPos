using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SalekhPos.Devices.Application.Devices;

namespace SalekhPos.IntegrationTests;

internal sealed class TrustedDeviceTestClient(Guid deviceId, Guid credentialId, ECDsa key) : IDisposable
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public Guid DeviceId { get; } = deviceId;
    public Guid CredentialId { get; } = credentialId;
    public ECDsa Key { get; } = key;

    public static async Task<TrustedDeviceTestClient> EnrollAsync(AccessFixture fixture, HttpClient client,
        Guid registerId, bool trust = true)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            using var registration = new HttpRequestMessage(HttpMethod.Post,
                $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/devices")
            {
                Content = JsonContent.Create(new
                {
                    registerId,
                    code = "SHIFT-DEVICE-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                    name = "Shift proof terminal",
                    platform = "desktop",
                    syncProtocolVersion = 1,
                    publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
                })
            };
            registration.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            using var registered = await client.SendAsync(registration);
            registered.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
            var deviceId = body.RootElement.GetProperty("id").GetGuid();
            var credential = body.RootElement.GetProperty("credential");
            var credentialId = credential.GetProperty("id").GetGuid();
            var challenge = credential.GetProperty("proofChallenge").GetString()!;
            var result = new TrustedDeviceTestClient(deviceId, credentialId, key);
            if (!trust) return result;

            var operationId = Guid.NewGuid();
            var fingerprint = SHA256.HashData(key.ExportSubjectPublicKeyInfo());
            var proof = Encoding.UTF8.GetBytes($"salekhpos-device-trust-v1\n{fixture.OrganizationA:D}\n{fixture.BranchA:D}\n{deviceId:D}\n{credentialId:D}\n{operationId:D}\n{challenge}\n{Convert.ToBase64String(fingerprint)}");
            var signature = Convert.ToBase64String(key.SignData(proof, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            using var trustRequest = new HttpRequestMessage(HttpMethod.Post,
                $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/devices/{deviceId:D}/trust")
            { Content = JsonContent.Create(new { credentialId, proofChallenge = challenge, signature }) };
            trustRequest.Headers.Add("Idempotency-Key", operationId.ToString("D"));
            using var trusted = await client.SendAsync(trustRequest);
            trusted.EnsureSuccessStatusCode();
            return result;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public async Task<HttpResponseMessage> OpenShiftAsync(AccessFixture fixture, HttpClient client, Guid registerId,
        Guid operationId, decimal openingBalance = 0m, string currency = "GEL", ShiftProofOverrides? overrides = null)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { registerId, currency, openingBalance }, WebJson);
        return await OpenShiftRawAsync(fixture, client, operationId, body, overrides);
    }

    public async Task<HttpResponseMessage> OpenShiftRawAsync(AccessFixture fixture, HttpClient client, Guid operationId,
        byte[] body, ShiftProofOverrides? overrides = null)
    {
        overrides ??= new();
        var path = $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/shifts/open";
        var timestamp = (overrides.Timestamp ?? DateTimeOffset.UtcNow).ToUniversalTime()
            .ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture);
        var nonceBytes = overrides.Nonce ?? RandomNumberGenerator.GetBytes(32);
        var nonce = Convert.ToBase64String(nonceBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var headerDeviceId = overrides.HeaderDeviceId ?? DeviceId;
        var headerCredentialId = overrides.HeaderCredentialId ?? CredentialId;
        var signedBody = overrides.SignedBody ?? body;
        var canonical = DeviceRequestProofCanonicalizer.Create("POST", overrides.SignedPath ?? path,
            fixture.OrganizationA, fixture.BranchA, overrides.SignedDeviceId ?? headerDeviceId,
            overrides.SignedCredentialId ?? headerCredentialId,
            overrides.SignedOperationIdentity ?? $"shift-open:{operationId:D}",
            Convert.ToHexString(SHA256.HashData(signedBody)), timestamp, nonce,
            SHA256.HashData(Key.ExportSubjectPublicKeyInfo()));
        var signature = Convert.ToBase64String((overrides.SigningKey ?? Key).SignData(canonical,
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        request.Headers.Add("X-SalekhPos-Device-Id", headerDeviceId.ToString("D"));
        request.Headers.Add("X-SalekhPos-Device-Credential", headerCredentialId.ToString("D"));
        request.Headers.Add("X-SalekhPos-Device-Timestamp", timestamp);
        request.Headers.Add("X-SalekhPos-Device-Nonce", nonce);
        request.Headers.Add("X-SalekhPos-Device-Signature", signature);
        return await client.SendAsync(request);
    }

    public async Task RevokeAsync(AccessFixture fixture, HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/devices/{DeviceId:D}/revoke")
        { Content = JsonContent.Create(new { reason = "Shift proof test revocation" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => Key.Dispose();
}

internal sealed record ShiftProofOverrides(DateTimeOffset? Timestamp = null, byte[]? Nonce = null,
    ECDsa? SigningKey = null, byte[]? SignedBody = null, string? SignedPath = null,
    string? SignedOperationIdentity = null, Guid? HeaderDeviceId = null, Guid? SignedDeviceId = null,
    Guid? HeaderCredentialId = null, Guid? SignedCredentialId = null);
