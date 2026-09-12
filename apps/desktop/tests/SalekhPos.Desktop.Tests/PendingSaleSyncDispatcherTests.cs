using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class PendingSaleSyncDispatcherTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DispatchesInOrderAndPersistsOnlyMatchingAcknowledgements()
    {
        var deviceId = Guid.NewGuid();
        var messages = new[] { Message(deviceId, 1), Message(deviceId, 2) };
        var store = new Store(messages);
        var dispatcher = new PendingSaleSyncDispatcher(store, new Transport(message => Ack(message)));

        Assert.Equal(2, await dispatcher.DispatchAsync(Guid.NewGuid(), Guid.NewGuid(), deviceId, 10, default));
        Assert.Equal([1L, 2L], store.Marked.Select(x => x.Sequence));
    }

    [Fact]
    public async Task MismatchedAcknowledgementLeavesMessagePending()
    {
        var message = Message(Guid.NewGuid(), 1);
        var store = new Store([message]);
        var dispatcher = new PendingSaleSyncDispatcher(store,
            new Transport(value => Ack(value) with { PayloadDigest = new string('0', 64) }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(
            Guid.NewGuid(), Guid.NewGuid(), message.DeviceId, 10, default));
        Assert.Empty(store.Marked);
    }

    [Theory]
    [InlineData("applied", "shift_conflict")]
    [InlineData("rejected", "applied")]
    public async Task ContradictoryStatusAndResultCodeLeaveMessagePending(string status, string resultCode)
    {
        var message = Message(Guid.NewGuid(), 1);
        var store = new Store([message]);
        var dispatcher = new PendingSaleSyncDispatcher(store,
            new Transport(value => Ack(value) with { Status = status, ResultCode = resultCode }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(
            Guid.NewGuid(), Guid.NewGuid(), message.DeviceId, 10, default));
        Assert.Empty(store.Marked);
    }

    [Fact]
    public async Task HttpTransportSendsExactSignedBodyAndServerCompatibleProof()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var message = Message(Guid.NewGuid(), 7);
        using var keys = new TestKeyProvider();
        var credentialId = Guid.NewGuid();
        var fingerprint = Convert.ToBase64String(SHA256.HashData(keys.PublicKey));
        var material = new ActiveDeviceProvisioningProofMaterial(organizationId, branchId, message.DeviceId,
            credentialId, TestKeyProvider.Reference, fingerprint);
        CapturedRequest? captured = null;
        var client = new HttpClient(new Handler(async request =>
        {
            captured = await Capture(request);
            var body = JsonSerializer.Serialize(Ack(message), JsonOptions);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }))
        { BaseAddress = new Uri("https://pos.test/") };
        var signer = new DeviceRequestProofSigner(keys, new ProofMaterialReader(material),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 13, 10, 11, 12, TimeSpan.Zero).AddTicks(3456789)));

        var result = await new HttpRemoteSyncTransport(client, signer).SendAsync(
            organizationId, branchId, message, default);

        Assert.Equal(message.MessageId, result.MessageId);
        var path = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{message.DeviceId:D}/sync/messages";
        Assert.Equal(path, captured!.Path);
        Assert.Equal("POST", captured.Method);
        var expectedBody = Encoding.UTF8.GetBytes($"{{\"messageId\":\"{message.MessageId:D}\",\"sequence\":7," +
            "\"protocolVersion\":1,\"messageType\":\"sale.completed.v1\",\"payload\":\"{}\"}");
        Assert.Equal(expectedBody, captured.Body);
        Assert.Equal(credentialId.ToString("D"), captured.Credential);
        Assert.Equal("2026-09-13T10:11:12.3456789Z", captured.Timestamp);
        Assert.Equal(32, DecodeBase64Url(captured.Nonce).Length);
        var signature = Convert.FromBase64String(captured.Signature);
        Assert.Equal(64, signature.Length);
        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(keys.PublicKey, out var read);
        Assert.Equal(keys.PublicKey.Length, read);
        Assert.True(verifier.VerifyData(Canonical(captured, organizationId, branchId, message.DeviceId,
                credentialId, message.MessageId, fingerprint), signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Theory]
    [InlineData("body")]
    [InlineData("path")]
    [InlineData("method")]
    [InlineData("operation")]
    [InlineData("credential")]
    [InlineData("fingerprint")]
    public async Task RequestProofRejectsCanonicalFieldTampering(string field)
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var message = Message(Guid.NewGuid(), 7);
        using var keys = new TestKeyProvider();
        var credentialId = Guid.NewGuid();
        var fingerprint = Convert.ToBase64String(SHA256.HashData(keys.PublicKey));
        var material = new ActiveDeviceProvisioningProofMaterial(organizationId, branchId, message.DeviceId,
            credentialId, TestKeyProvider.Reference, fingerprint);
        CapturedRequest? captured = null;
        using var client = new HttpClient(new Handler(async request =>
        {
            captured = await Capture(request);
            return Response(Ack(message));
        })) { BaseAddress = new Uri("https://pos.test/") };
        await new HttpRemoteSyncTransport(client, new DeviceRequestProofSigner(keys,
            new ProofMaterialReader(material))).SendAsync(organizationId, branchId, message, default);

        var changed = captured! with
        {
            Body = field == "body" ? [.. captured!.Body, (byte)' '] : captured.Body,
            Path = field == "path" ? captured.Path + "/changed" : captured.Path,
            Method = field == "method" ? "PUT" : captured.Method,
        };
        var verifiedCredential = field == "credential" ? Guid.NewGuid() : credentialId;
        var verifiedMessage = field == "operation" ? Guid.NewGuid() : message.MessageId;
        var verifiedFingerprint = field == "fingerprint" ? Convert.ToBase64String(new byte[32]) : fingerprint;
        using var verifier = ECDsa.Create(); verifier.ImportSubjectPublicKeyInfo(keys.PublicKey, out _);
        Assert.False(verifier.VerifyData(Canonical(changed, organizationId, branchId, message.DeviceId,
                verifiedCredential, verifiedMessage, verifiedFingerprint),
            Convert.FromBase64String(captured.Signature), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public async Task EveryRetryUsesFreshProofAndPreservesExactBodyBytes()
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var message = Message(Guid.NewGuid(), 7);
        using var keys = new TestKeyProvider();
        var material = new ActiveDeviceProvisioningProofMaterial(organizationId, branchId, message.DeviceId,
            Guid.NewGuid(), TestKeyProvider.Reference, Convert.ToBase64String(SHA256.HashData(keys.PublicKey)));
        var captured = new List<CapturedRequest>();
        using var client = new HttpClient(new Handler(async request =>
        {
            captured.Add(await Capture(request));
            return captured.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Response(Ack(message));
        })) { BaseAddress = new Uri("https://pos.test/") };
        var transport = new HttpRemoteSyncTransport(client, new DeviceRequestProofSigner(keys,
            new ProofMaterialReader(material), new AdvancingTimeProvider()));

        await Assert.ThrowsAsync<HttpRequestException>(() => transport.SendAsync(
            organizationId, branchId, message, default));
        await transport.SendAsync(organizationId, branchId, message, default);

        Assert.Equal(2, captured.Count);
        Assert.Equal(captured[0].Body, captured[1].Body);
        Assert.NotEqual(captured[0].Nonce, captured[1].Nonce);
        Assert.NotEqual(captured[0].Timestamp, captured[1].Timestamp);
        Assert.NotEqual(captured[0].Signature, captured[1].Signature);
    }

    [Theory]
    [InlineData("missing-state")]
    [InlineData("inactive-state")]
    [InlineData("corrupt-state")]
    [InlineData("missing-key")]
    [InlineData("fingerprint-mismatch")]
    [InlineData("invalid-signature")]
    public async Task InvalidProvisioningProofMaterialFailsClosedBeforeSending(string failure)
    {
        var organizationId = Guid.NewGuid(); var branchId = Guid.NewGuid(); var message = Message(Guid.NewGuid(), 7);
        using var keys = new TestKeyProvider
        {
            Missing = failure == "missing-key",
            InvalidSignature = failure == "invalid-signature",
        };
        var fingerprint = failure == "fingerprint-mismatch"
            ? Convert.ToBase64String(new byte[32])
            : Convert.ToBase64String(SHA256.HashData(keys.PublicKey));
        var material = new ActiveDeviceProvisioningProofMaterial(organizationId, branchId, message.DeviceId,
            Guid.NewGuid(), TestKeyProvider.Reference, fingerprint);
        var reader = failure is "missing-state" or "inactive-state" or "corrupt-state"
            ? new ProofMaterialReader(new InvalidOperationException(failure))
            : new ProofMaterialReader(material);
        var sends = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            sends++;
            return Task.FromResult(Response(Ack(message)));
        })) { BaseAddress = new Uri("https://pos.test/") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpRemoteSyncTransport(client,
            new DeviceRequestProofSigner(keys, reader)).SendAsync(organizationId, branchId, message, default));

        Assert.Equal(0, sends);
    }

    private static byte[] Canonical(CapturedRequest request, Guid organizationId, Guid branchId, Guid deviceId,
        Guid credentialId, Guid messageId, string fingerprint) => Encoding.UTF8.GetBytes(string.Join('\n',
        "salekhpos-device-request-v1", request.Method, request.Path, organizationId.ToString("D"),
        branchId.ToString("D"), deviceId.ToString("D"), credentialId.ToString("D"), $"message:{messageId:D}",
        Convert.ToHexString(SHA256.HashData(request.Body)), request.Timestamp, request.Nonce, fingerprint));

    private static async Task<CapturedRequest> Capture(HttpRequestMessage request) => new(
        request.Method.Method, request.RequestUri!.AbsolutePath, await request.Content!.ReadAsByteArrayAsync(),
        request.Headers.GetValues("X-SalekhPos-Device-Credential").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Timestamp").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Nonce").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Signature").Single());

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static HttpResponseMessage Response(RemoteSyncAcknowledgement acknowledgement) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(acknowledgement, JsonOptions), Encoding.UTF8,
            "application/json"),
    };

    private sealed record CapturedRequest(string Method, string Path, byte[] Body, string Credential,
        string Timestamp, string Nonce, string Signature);

    private sealed class ProofMaterialReader : IActiveDeviceProvisioningProofMaterialReader
    {
        private readonly ActiveDeviceProvisioningProofMaterial? material;
        private readonly Exception? exception;
        public ProofMaterialReader(ActiveDeviceProvisioningProofMaterial material) => this.material = material;
        public ProofMaterialReader(Exception exception) => this.exception = exception;
        public Task<ActiveDeviceProvisioningProofMaterial> ReadActiveAsync(CancellationToken cancellationToken) =>
            exception is null ? Task.FromResult(material!)
                : Task.FromException<ActiveDeviceProvisioningProofMaterial>(exception);
    }

    private sealed class TestKeyProvider : IDeviceSigningKeyProvider, IDisposable
    {
        public const string Reference = "test-key";
        private readonly byte[] privateKey;
        public byte[] PublicKey { get; }
        public bool Missing { get; init; }
        public bool InvalidSignature { get; init; }

        public TestKeyProvider()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            privateKey = key.ExportPkcs8PrivateKey();
            PublicKey = key.ExportSubjectPublicKeyInfo();
        }

        public string CreateKeyReference() => throw new NotSupportedException();
        public IDeviceSigningKey Open(string keyReference, bool createIfMissing)
        {
            Assert.Equal(Reference, keyReference);
            Assert.False(createIfMissing);
            if (Missing) throw new InvalidOperationException("missing key");
            var key = ECDsa.Create(); key.ImportPkcs8PrivateKey(privateKey, out _);
            return new TestKey(key, InvalidSignature);
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(privateKey);
    }

    private sealed class TestKey(ECDsa key, bool invalidSignature) : IDeviceSigningKey
    {
        public byte[] GetSubjectPublicKeyInfo() => key.ExportSubjectPublicKeyInfo();
        public byte[] Sign(ReadOnlySpan<byte> data) => invalidSignature ? new byte[64] : key.SignData(data,
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() => key.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long ticks;
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Increment(ref ticks));
    }

    private static LocalOutboxMessage Message(Guid deviceId, long sequence) => new(Guid.NewGuid(), Guid.NewGuid(),
        deviceId, sequence, "sale.completed.v1", "{}", new string('A', 64), "pending", null, DateTimeOffset.UtcNow);
    private static RemoteSyncAcknowledgement Ack(LocalOutboxMessage message) => new(message.MessageId,
        message.DeviceId, message.SaleId, message.Sequence, 1, message.MessageType, "applied", "applied",
        message.PayloadDigest, DateTimeOffset.UtcNow, false);

    private sealed class Transport(Func<LocalOutboxMessage, RemoteSyncAcknowledgement> response) : IRemoteSyncTransport
    {
        public Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
            LocalOutboxMessage message, CancellationToken cancellationToken) => Task.FromResult(response(message));
    }

    private sealed class Store(IReadOnlyList<LocalOutboxMessage> pending) : ILocalSaleStore
    {
        public List<LocalOutboxMessage> Marked { get; } = [];
        public Task<LocalSaleWriteResult> CompleteAsync(CompleteLocalSaleCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LocalOutboxMessage>> ReadPendingAsync(Guid deviceId, int limit, CancellationToken cancellationToken) => Task.FromResult(pending);
        public Task MarkResultAsync(Guid messageId, string payloadDigest, string status, string resultCode,
            DateTimeOffset acceptedAt, CancellationToken cancellationToken)
        {
            Marked.Add(pending.Single(x => x.MessageId == messageId));
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => response(request);
    }
}
