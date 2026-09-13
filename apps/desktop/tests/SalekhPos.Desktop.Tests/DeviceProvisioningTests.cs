using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Infrastructure.Devices;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class DeviceProvisioningTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProvisioningSignsExactProofAndIsIdempotent()
    {
        var path = TempDatabase();
        using var keys = new TestKeyProvider();
        var client = new ServerClient();
        var request = Request();
        var clock = new FixedTimeProvider(ServerClient.Now);
        try
        {
            var first = new DeviceProvisioner(keys, new SqliteDeviceProvisioningStateStore(path), client, clock);
            var device = await first.ProvisionAsync(request);
            var second = new DeviceProvisioner(keys, new SqliteDeviceProvisioningStateStore(path), client, clock);

            var replay = await second.ProvisionAsync(request);

            Assert.Equal("active", device.Status);
            Assert.Equal(device, replay);
            Assert.Equal(1, client.RegisterCalls);
            Assert.Equal(1, client.TrustCalls);
            Assert.Equal(64, client.Signature!.Length);
            Assert.True(client.SignatureVerified);
            Assert.Equal(client.RegisterOperationId, client.ObservedRegistrationOperationId);
            Assert.Equal(client.TrustOperationId, client.ObservedTrustOperationId);
            var databaseBytes = await File.ReadAllBytesAsync(path);
            Assert.DoesNotContain(Convert.ToBase64String(client.Signature), Encoding.UTF8.GetString(databaseBytes));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PendingProvisioningResumesWithStableIdsAndOperationsAfterRestart()
    {
        var path = TempDatabase();
        using var keys = new TestKeyProvider();
        var client = new ServerClient { FailFirstTrust = true };
        var request = Request();
        var clock = new FixedTimeProvider(ServerClient.Now);
        try
        {
            var first = new DeviceProvisioner(keys, new SqliteDeviceProvisioningStateStore(path), client, clock);
            await Assert.ThrowsAsync<HttpRequestException>(() => first.ProvisionAsync(request));
            var originalDevice = client.DeviceId;
            var originalRegistrationOperation = client.ObservedRegistrationOperationId;
            var originalTrustOperation = client.ObservedTrustOperationId;

            var resumed = new DeviceProvisioner(keys, new SqliteDeviceProvisioningStateStore(path), client, clock);
            var device = await resumed.ProvisionAsync(request);

            Assert.Equal(originalDevice, device.Id);
            Assert.Equal(1, client.RegisterCalls);
            Assert.Equal(2, client.TrustCalls);
            Assert.Equal(originalRegistrationOperation, client.ObservedRegistrationOperationId);
            Assert.Equal(originalTrustOperation, client.ObservedTrustOperationId);
            Assert.Equal(1, keys.CreatedKeys);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ChangedTrustResponseIsRejectedAndPendingEvidenceIsRetained()
    {
        var path = TempDatabase();
        using var keys = new TestKeyProvider();
        var client = new ServerClient { ChangeTrustResponse = true };
        var request = Request();
        var store = new SqliteDeviceProvisioningStateStore(path);
        try
        {
            var provisioner = new DeviceProvisioner(keys, store, client, new FixedTimeProvider(ServerClient.Now));

            await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(request));

            var state = await store.GetOrCreateAsync(request, Guid.NewGuid(), Guid.NewGuid(), "ignored", default);
            Assert.Equal("pending", state.Device!.Status);
            Assert.Equal("pending", state.Device.Credential!.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ActiveProvisioningFailsClosedWhenPersistedKeyIsMissing()
    {
        var path = TempDatabase();
        var request = Request();
        var client = new ServerClient();
        try
        {
            using (var originalKeys = new TestKeyProvider())
                await new DeviceProvisioner(originalKeys, new SqliteDeviceProvisioningStateStore(path), client,
                    new FixedTimeProvider(ServerClient.Now)).ProvisionAsync(request);
            using var emptyKeys = new TestKeyProvider();
            var reopened = new DeviceProvisioner(emptyKeys, new SqliteDeviceProvisioningStateStore(path), client,
                new FixedTimeProvider(ServerClient.Now));

            await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ProvisionAsync(request));
            Assert.Equal(1, client.RegisterCalls);
            Assert.Equal(1, client.TrustCalls);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ActiveProofMaterialReaderRejectsMissingPendingAndCorruptState()
    {
        var path = TempDatabase();
        var request = Request();
        var store = new SqliteDeviceProvisioningStateStore(path);
        var credentialId = Guid.NewGuid();
        var fingerprint = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadActiveAsync(default));
            var state = await store.GetOrCreateAsync(request, Guid.NewGuid(), Guid.NewGuid(),
                "cng-user:" + Guid.NewGuid().ToString("N"), default);
            state = await store.BindPublicKeyAsync(state, fingerprint, default);
            var pending = new ProvisionedDevice(Guid.NewGuid(), request.BranchId, request.RegisterId, request.Code,
                request.Name, request.Platform, "pending", 1, ServerClient.Now, "operator-1",
                new DeviceCredential(credentialId, "ecdsa-p256-sha256", Convert.ToBase64String(new byte[32]),
                    ServerClient.Now.AddMinutes(15), "pending"), null, null, null);
            state = await store.RecordRegistrationAsync(state, pending, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadActiveAsync(default));
            var active = pending with
            {
                Status = "active",
                Credential = pending.Credential! with { Status = "active" },
            };
            await store.RecordTrustAsync(state, active, default);

            var material = await store.ReadActiveAsync(default);

            Assert.Equal(request.OrganizationId, material.OrganizationId);
            Assert.Equal(request.BranchId, material.BranchId);
            Assert.Equal(active.Id, material.DeviceId);
            Assert.Equal(credentialId, material.CredentialId);
            Assert.Equal(fingerprint, material.PublicKeyFingerprint);
            await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE device_provisioning_state SET public_key_fingerprint='corrupt'";
                await command.ExecuteNonQueryAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadActiveAsync(default));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ProofUsesCanonicalServerCompatibleBytes()
    {
        var organization = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var branch = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var device = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var credential = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var operation = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var challenge = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        var fingerprint = Convert.ToBase64String(Enumerable.Range(32, 32).Select(i => (byte)i).ToArray());

        var proof = DeviceProvisioner.BuildProof(organization, branch, device, credential, operation,
            challenge, fingerprint);

        Assert.Equal("salekhpos-device-trust-v1\n11111111-1111-4111-8111-111111111111\n" +
            "22222222-2222-4222-8222-222222222222\n33333333-3333-4333-8333-333333333333\n" +
            "44444444-4444-4444-8444-444444444444\n55555555-5555-4555-8555-555555555555\n" +
            $"{challenge}\n{fingerprint}", Encoding.UTF8.GetString(proof));
    }

    [Fact]
    public async Task HttpClientUsesVersionedScopedIdempotentRegistrationContract()
    {
        var request = Request();
        var operation = Guid.NewGuid();
        HttpRequestMessage? captured = null;
        string? body = null;
        var device = new ProvisionedDevice(Guid.NewGuid(), request.BranchId, request.RegisterId, request.Code,
            request.Name, request.Platform, "pending", 1, ServerClient.Now, "operator-1",
            new DeviceCredential(Guid.NewGuid(), "ecdsa-p256-sha256",
                Convert.ToBase64String(new byte[32]), ServerClient.Now.AddMinutes(15), "pending"), null, null, null);
        using var http = new HttpClient(new CapturingHandler(async message =>
        {
            captured = message;
            body = await message.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(device, WebJsonOptions), Encoding.UTF8, "application/json"),
            };
        }))
        { BaseAddress = new Uri("https://pos.test/") };

        var result = await new HttpDeviceProvisioningClient(http).RegisterAsync(request, operation, "public-key", default);

        Assert.Equal(device, result);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal($"api/v1/organizations/{request.OrganizationId:D}/branches/{request.BranchId:D}/devices",
            captured.RequestUri!.PathAndQuery.TrimStart('/'));
        Assert.Equal(operation.ToString("D"), captured.Headers.GetValues("Idempotency-Key").Single());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(request.RegisterId, json.RootElement.GetProperty("registerId").GetGuid());
        Assert.Equal("public-key", json.RootElement.GetProperty("publicKey").GetString());
    }

    private static DeviceProvisioningRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "POS-01", "Front register", "windows", 1);

    private static string TempDatabase() => Path.Combine(Path.GetTempPath(), $"salekhpos-device-{Guid.NewGuid():N}.db");

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }

    private sealed class TestKeyProvider : IDeviceSigningKeyProvider, IDisposable
    {
        private readonly Dictionary<string, byte[]> privateKeys = [];
        public int CreatedKeys { get; private set; }
        public string CreateKeyReference() => "test:" + Guid.NewGuid().ToString("N");

        public IDeviceSigningKey Open(string keyReference, bool createIfMissing)
        {
            if (!privateKeys.TryGetValue(keyReference, out var material))
            {
                if (!createIfMissing) throw new InvalidOperationException("missing");
                using var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                material = created.ExportPkcs8PrivateKey();
                privateKeys.Add(keyReference, material); CreatedKeys++;
            }
            var key = ECDsa.Create(); key.ImportPkcs8PrivateKey(material, out _);
            return new TestKey(key);
        }

        public void Dispose()
        {
            foreach (var material in privateKeys.Values) CryptographicOperations.ZeroMemory(material);
            privateKeys.Clear();
        }
    }

    private sealed class TestKey(ECDsa key) : IDeviceSigningKey
    {
        public byte[] GetSubjectPublicKeyInfo() => key.ExportSubjectPublicKeyInfo();
        public byte[] Sign(ReadOnlySpan<byte> data) => key.SignData(data, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() => key.Dispose();
    }

    private sealed class ServerClient : IDeviceProvisioningClient
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);
        private ProvisionedDevice? pending;
        private byte[]? publicKey;
        public int RegisterCalls { get; private set; }
        public int TrustCalls { get; private set; }
        public Guid DeviceId => pending!.Id;
        public Guid RegisterOperationId { get; private set; }
        public Guid TrustOperationId { get; private set; }
        public Guid ObservedRegistrationOperationId { get; private set; }
        public Guid ObservedTrustOperationId { get; private set; }
        public byte[]? Signature { get; private set; }
        public bool SignatureVerified { get; private set; }
        public bool FailFirstTrust { get; init; }
        public bool ChangeTrustResponse { get; init; }

        public Task<ProvisionedDevice> RegisterAsync(DeviceProvisioningRequest request, Guid operationId,
            string publicKeyValue, CancellationToken cancellationToken)
        {
            RegisterCalls++; ObservedRegistrationOperationId = operationId;
            RegisterOperationId = RegisterOperationId == Guid.Empty ? operationId : RegisterOperationId;
            Assert.Equal(RegisterOperationId, operationId);
            publicKey = Convert.FromBase64String(publicKeyValue);
            pending ??= new ProvisionedDevice(Guid.NewGuid(), request.BranchId, request.RegisterId, request.Code,
                request.Name, request.Platform, "pending", request.SyncProtocolVersion, Now, "operator-1",
                new DeviceCredential(Guid.NewGuid(), "ecdsa-p256-sha256",
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), Now.AddMinutes(15), "pending"),
                null, null, null);
            return Task.FromResult(pending);
        }

        public Task<ProvisionedDevice> TrustAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid operationId, Guid credentialId, string proofChallenge, string signature,
            CancellationToken cancellationToken)
        {
            TrustCalls++; ObservedTrustOperationId = operationId;
            TrustOperationId = TrustOperationId == Guid.Empty ? operationId : TrustOperationId;
            Assert.Equal(TrustOperationId, operationId);
            Assert.Equal(pending!.Id, deviceId); Assert.Equal(pending.Credential!.Id, credentialId);
            var fingerprint = Convert.ToBase64String(SHA256.HashData(publicKey!));
            var proof = DeviceProvisioner.BuildProof(organizationId, branchId, deviceId, credentialId,
                operationId, proofChallenge, fingerprint);
            Signature = Convert.FromBase64String(signature);
            using var verifier = ECDsa.Create(); verifier.ImportSubjectPublicKeyInfo(publicKey!, out _);
            SignatureVerified = verifier.VerifyData(proof, Signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (FailFirstTrust && TrustCalls == 1) throw new HttpRequestException("uncertain result");
            var active = pending with { Status = "active", Credential = pending.Credential with { Status = "active" } };
            if (ChangeTrustResponse) active = active with { RegisteredBy = "changed-operator" };
            return Task.FromResult(active);
        }
    }
}
