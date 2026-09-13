using System.Net;
using System.Text;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Infrastructure.Sync;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class CashManagementHttpTests
{
    [Fact]
    public async Task OpenSendsExactBodyAndTrustedHeadersWithFreshProofOnStableRetry()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var operation = Guid.NewGuid(); var shift = Guid.NewGuid();
        var openedAt = new DateTimeOffset(2026, 9, 13, 12, 30, 0, TimeSpan.Zero);
        var captured = new List<Captured>(); var signer = new ProofSigner();
        using var client = Client(request =>
        {
            captured.Add(Capture(request));
            return captured.Count == 1 ? new(HttpStatusCode.ServiceUnavailable) : Json($$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{register:D}}","status":"open","currency":"GEL","openingBalance":12.345678,"openedAt":"{{openedAt:O}}","openedBy":"cashier-1"}""");
        });
        var remote = new HttpRemoteCashManagement(client, signer);
        var intent = new OpenCashSessionRequest(register, "GEL", 12.345678m, operation);

        await Assert.ThrowsAsync<HttpRequestException>(() => remote.OpenAsync(organization, branch, device,
            intent, default));
        var result = await remote.OpenAsync(organization, branch, device, intent, default);

        var expected = Encoding.UTF8.GetBytes($$"""{"registerId":"{{register:D}}","currency":"GEL","openingBalance":12.345678}""");
        Assert.Equal(shift, result.ShiftId); Assert.Equal(2, captured.Count);
        Assert.All(captured, value =>
        {
            Assert.Equal(expected, value.Body);
            Assert.Equal(operation.ToString("D"), value.IdempotencyKey);
            Assert.Equal(device.ToString("D"), value.DeviceId);
            Assert.Equal(signer.CredentialId.ToString("D"), value.CredentialId);
        });
        Assert.Equal(signer.Calls[0].Body, signer.Calls[1].Body);
        Assert.Equal(operation, signer.Calls[0].OperationId); Assert.Equal(operation, signer.Calls[1].OperationId);
        Assert.NotEqual(captured[0].Nonce, captured[1].Nonce);
        Assert.NotEqual(captured[0].Signature, captured[1].Signature);
    }

    [Theory]
    [InlineData("branch")]
    [InlineData("register")]
    [InlineData("status")]
    [InlineData("currency")]
    [InlineData("balance")]
    [InlineData("openedAt")]
    [InlineData("openedBy")]
    public async Task OpenRejectsMismatchedOrInvalidServerEvidence(string field)
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid();
        var responseBranch = field == "branch" ? Guid.NewGuid() : branch;
        var responseRegister = field == "register" ? Guid.NewGuid() : register;
        var status = field == "status" ? "closed" : "open";
        var currency = field == "currency" ? "USD" : "GEL";
        var balance = field == "balance" ? "2" : "1";
        var openedAt = field == "openedAt" ? "2026-09-13T12:00:00+04:00" : "2026-09-13T08:00:00Z";
        var openedBy = field == "openedBy" ? " " : "cashier";
        using var client = Client(_ => Json($$"""{"id":"{{shift:D}}","branchId":"{{responseBranch:D}}","registerId":"{{responseRegister:D}}","status":"{{status}}","currency":"{{currency}}","openingBalance":{{balance}},"openedAt":"{{openedAt}}","openedBy":"{{openedBy}}"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpRemoteCashManagement(client,
            new ProofSigner()).OpenAsync(organization, branch, device,
            new(register, "GEL", 1m, Guid.NewGuid()), default));
    }
    [Fact]
    public async Task MovementSendsExactBodyAndTrustedHeadersWithFreshProofOnStableRetry()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var operation = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 9, 13, 13, 0, 0, TimeSpan.Zero);
        var captured = new List<Captured>(); var signer = new ProofSigner();
        using var client = Client(request =>
        {
            captured.Add(Capture(request));
            return captured.Count == 1 ? new(HttpStatusCode.ServiceUnavailable) :
                Json($$"""{"id":"{{Guid.NewGuid():D}}","shiftId":"{{shift:D}}","kind":"cash_out","currency":"GEL","amount":3,"reason":"Petty cash","recordedAt":"{{recordedAt:O}}","recordedBy":"cashier"}""");
        });
        var remote = new HttpRemoteCashManagement(client, signer);

        await Assert.ThrowsAsync<HttpRequestException>(() => remote.RecordMovementAsync(organization, branch,
            device, shift, register, operation, "cash_out", 3m, "Petty cash", default));
        var result = await remote.RecordMovementAsync(organization, branch, device, shift, register, operation,
            "cash_out", 3m, "Petty cash", default);

        var expected = Encoding.UTF8.GetBytes(
            $$"""{"registerId":"{{register:D}}","kind":"cash_out","amount":3,"reason":"Petty cash"}""");
        Assert.Equal(3m, result.Amount); Assert.Equal(2, captured.Count);
        AssertSignedRetry(captured, signer, expected, operation, device, "cash-movement");
    }

    [Fact]
    public async Task MovementRejectsReasonShorterThanServerContractBeforeNetwork()
    {
        var sends = 0;
        using var client = Client(_ => { sends++; return new(HttpStatusCode.OK); });
        var remote = new HttpRemoteCashManagement(client, new ProofSigner());
        await Assert.ThrowsAsync<ArgumentException>(() => remote.RecordMovementAsync(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cash_in", 1m, "x", default));
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task CloseSendsExactBodyAndTrustedHeadersWithFreshProofOnStableRetry()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid(); var operation = Guid.NewGuid();
        var openedAt = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        var closedAt = openedAt.AddHours(1); var captured = new List<Captured>(); var signer = new ProofSigner();
        using var client = Client(request =>
        {
            captured.Add(Capture(request));
            return captured.Count == 1 ? new(HttpStatusCode.ServiceUnavailable) :
                Json($$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{register:D}}","status":"closed","currency":"GEL","openingBalance":2,"cashSales":10,"cashRefunds":1,"cashIn":3,"cashOut":4,"expectedCash":10,"countedCash":9,"variance":-1,"openedAt":"{{openedAt:O}}","closedAt":"{{closedAt:O}}","openedBy":"cashier","closedBy":"cashier"}""");
        });
        var remote = new HttpRemoteCashManagement(client, signer);

        await Assert.ThrowsAsync<HttpRequestException>(() => remote.CloseAsync(organization, branch, device,
            shift, register, operation, 9m, default));
        var result = await remote.CloseAsync(organization, branch, device, shift, register, operation, 9m, default);

        var expected = Encoding.UTF8.GetBytes($$"""{"registerId":"{{register:D}}","countedCash":9}""");
        Assert.Equal(-1m, result.Variance); Assert.Equal(2, captured.Count);
        AssertSignedRetry(captured, signer, expected, operation, device, "shift-close");
    }

    [Fact]
    public async Task CloseRejectsMismatchedServerEvidence()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var device = Guid.NewGuid();
        var register = Guid.NewGuid(); var shift = Guid.NewGuid();
        using var client = Client(_ => Json($$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{Guid.NewGuid():D}}","status":"closed","currency":"GEL","openingBalance":0,"cashSales":0,"cashRefunds":0,"cashIn":0,"cashOut":0,"expectedCash":0,"countedCash":10,"variance":10,"openedAt":"2026-09-13T08:00:00Z","closedAt":"2026-09-13T09:00:00Z","openedBy":"cashier","closedBy":"cashier"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpRemoteCashManagement(client,
            new ProofSigner()).CloseAsync(organization, branch, device, shift, register, Guid.NewGuid(), 10m,
            default));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response))
    { BaseAddress = new("https://pos.test/") };
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
    private static Captured Capture(HttpRequestMessage request) => new(
        request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult(),
        request.Headers.GetValues("Idempotency-Key").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Id").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Credential").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Timestamp").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Nonce").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Signature").Single());
    private sealed record Captured(byte[] Body, string IdempotencyKey, string DeviceId, string CredentialId,
        string Timestamp, string Nonce, string Signature);
    private static void AssertSignedRetry(IReadOnlyList<Captured> captured, ProofSigner signer, byte[] expected,
        Guid operation, Guid device, string operationType)
    {
        Assert.All(captured, value =>
        {
            Assert.Equal(expected, value.Body);
            Assert.Equal(operation.ToString("D"), value.IdempotencyKey);
            Assert.Equal(device.ToString("D"), value.DeviceId);
            Assert.Equal(signer.CredentialId.ToString("D"), value.CredentialId);
        });
        Assert.Equal(expected, signer.Calls[0].Body); Assert.Equal(expected, signer.Calls[1].Body);
        Assert.All(signer.Calls, call =>
        {
            Assert.Equal(operation, call.OperationId); Assert.Equal(operationType, call.OperationType);
        });
        Assert.NotEqual(captured[0].Timestamp, captured[1].Timestamp);
        Assert.NotEqual(captured[0].Nonce, captured[1].Nonce);
        Assert.NotEqual(captured[0].Signature, captured[1].Signature);
    }
    private sealed class ProofSigner : IDeviceRequestProofSigner
    {
        private int attempt;
        public Guid CredentialId { get; } = Guid.NewGuid();
        public List<(Guid OperationId, string OperationType, string Path, byte[] Body)> Calls { get; } = [];
        public Task<DeviceRequestProof> SignShiftOpenAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid operationId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref attempt);
            return Proof(operationId, "shift-open", canonicalPath, body, current);
        }
        public Task<DeviceRequestProof> SignCashMovementAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid shiftId, Guid operationId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken) => Proof(operationId, "cash-movement", canonicalPath, body,
                Interlocked.Increment(ref attempt));
        public Task<DeviceRequestProof> SignShiftCloseAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid shiftId, Guid operationId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken) => Proof(operationId, "shift-close", canonicalPath, body,
                Interlocked.Increment(ref attempt));
        public Task<DeviceRequestProof> SignSyncMessageAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid messageId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        private Task<DeviceRequestProof> Proof(Guid operationId, string operationType, string canonicalPath,
            ReadOnlyMemory<byte> body, int current)
        {
            Calls.Add((operationId, operationType, canonicalPath, body.ToArray()));
            return Task.FromResult(new DeviceRequestProof(CredentialId.ToString("D"),
                $"2026-09-13T08:00:0{current}.0000000Z", $"nonce-{current}", $"signature-{current}"));
        }
    }
}
