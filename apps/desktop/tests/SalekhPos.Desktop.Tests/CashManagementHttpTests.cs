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
    public async Task MovementUsesScopedEndpointAndRetryStableOperationId()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var shift = Guid.NewGuid();
        var operation = Guid.NewGuid(); HttpRequestMessage? captured = null;
        using var client = Client(request =>
        {
            captured = request;
            return Json($$"""{"id":"{{Guid.NewGuid():D}}","shiftId":"{{shift:D}}","kind":"cash_out","currency":"GEL","amount":3,"reason":"Petty cash","recordedAt":"{{DateTimeOffset.UtcNow:O}}","recordedBy":"cashier"}""");
        });

        var result = await new HttpRemoteCashManagement(client).RecordMovementAsync(organization, branch, shift,
            operation, "cash_out", 3m, "Petty cash", default);

        Assert.Equal(3m, result.Amount); Assert.Equal(operation.ToString("D"),
            captured!.Headers.GetValues("Idempotency-Key").Single());
        Assert.EndsWith($"/shifts/{shift:D}/cash-movements", captured.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CloseRejectsMismatchedServerEvidence()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var shift = Guid.NewGuid();
        using var client = Client(_ => Json($$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{Guid.NewGuid():D}}","status":"closed","currency":"GEL","openingBalance":0,"cashSales":0,"cashRefunds":0,"cashIn":0,"cashOut":0,"expectedCash":0,"countedCash":99,"variance":99,"openedAt":"{{DateTimeOffset.UtcNow.AddHours(-1):O}}","closedAt":"{{DateTimeOffset.UtcNow:O}}","openedBy":"cashier","closedBy":"cashier"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpRemoteCashManagement(client).CloseAsync(
            organization, branch, shift, Guid.NewGuid(), 10m, default));
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
        request.Headers.GetValues("X-SalekhPos-Device-Nonce").Single(),
        request.Headers.GetValues("X-SalekhPos-Device-Signature").Single());
    private sealed record Captured(byte[] Body, string IdempotencyKey, string DeviceId, string CredentialId,
        string Nonce, string Signature);
    private sealed class ProofSigner : IDeviceRequestProofSigner
    {
        private int attempt;
        public Guid CredentialId { get; } = Guid.NewGuid();
        public List<(Guid OperationId, string Path, byte[] Body)> Calls { get; } = [];
        public Task<DeviceRequestProof> SignShiftOpenAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid operationId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref attempt);
            Calls.Add((operationId, canonicalPath, body.ToArray()));
            return Task.FromResult(new DeviceRequestProof(CredentialId.ToString("D"),
                $"2026-09-13T08:00:0{current}.0000000Z", $"nonce-{current}", $"signature-{current}"));
        }
        public Task<DeviceRequestProof> SignSyncMessageAsync(Guid organizationId, Guid branchId, Guid deviceId,
            Guid messageId, string canonicalPath, ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
