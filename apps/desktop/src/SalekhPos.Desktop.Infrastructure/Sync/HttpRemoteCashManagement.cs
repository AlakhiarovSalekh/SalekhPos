using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Shifts;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteCashManagement(HttpClient client, IDeviceRequestProofSigner? proofSigner = null)
    : IRemoteCashManagement
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenCashSessionResult> OpenAsync(Guid organizationId, Guid branchId, Guid deviceId,
        OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty
            || request.RegisterId == Guid.Empty || request.OperationId == Guid.Empty
            || request.Currency is null || request.Currency.Length != 3
            || request.Currency.Any(c => c is < 'A' or > 'Z') || request.OpeningBalance < 0
            || decimal.Round(request.OpeningBalance, 6) != request.OpeningBalance)
            throw new ArgumentException("Shift-opening request is invalid.");
        if (proofSigner is null)
            throw new InvalidOperationException("Trusted device request signing is not configured.");

        var path = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open";
        var body = JsonSerializer.SerializeToUtf8Bytes(new OpenRequestBody(request.RegisterId, request.Currency,
            request.OpeningBalance), JsonOptions);
        var proof = await proofSigner.SignShiftOpenAsync(organizationId, branchId, deviceId, request.OperationId,
            path, body, cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
        message.Headers.Add("Idempotency-Key", request.OperationId.ToString("D"));
        message.Headers.Add("X-SalekhPos-Device-Id", deviceId.ToString("D"));
        message.Headers.Add("X-SalekhPos-Device-Credential", proof.CredentialId);
        message.Headers.Add("X-SalekhPos-Device-Timestamp", proof.Timestamp);
        message.Headers.Add("X-SalekhPos-Device-Nonce", proof.Nonce);
        message.Headers.Add("X-SalekhPos-Device-Signature", proof.Signature);
        message.Content = new ByteArrayContent(body);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        { CharSet = Encoding.UTF8.WebName };
        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenResponseBody>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The shift-opening response is empty.");
        if (result.Id == Guid.Empty || result.BranchId != branchId || result.RegisterId != request.RegisterId
            || result.Status != "open" || result.Currency != request.Currency
            || result.OpeningBalance != request.OpeningBalance || result.OpenedAt == default
            || result.OpenedAt.Offset != TimeSpan.Zero || InvalidActor(result.OpenedBy))
            throw new InvalidOperationException("The shift-opening response does not match the request.");
        return new(result.Id, result.BranchId, result.RegisterId, result.Status, result.Currency,
            result.OpeningBalance, result.OpenedAt, result.OpenedBy);
    }

    public async Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid shiftId,
        Guid operationId, string kind, decimal amount, string reason, CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, shiftId, operationId);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/cash-movements");
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        request.Content = JsonContent.Create(new { kind, amount, reason });
        using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CashMovementResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The cash-movement response is empty.");
        if (result.Id == Guid.Empty || result.ShiftId != shiftId || result.Kind != kind || result.Amount != amount
            || result.Reason != reason || result.RecordedAt == default || result.RecordedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("The cash-movement response does not match the request.");
        return result;
    }

    public async Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid shiftId,
        Guid operationId, decimal countedCash, CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, shiftId, operationId);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/close");
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        request.Content = JsonContent.Create(new { countedCash });
        using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ClosedBody>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The shift-closing response is empty.");
        if (result.Id != shiftId || result.BranchId != branchId || result.Status != "closed"
            || result.CountedCash != countedCash || result.ClosedAt == default || result.ClosedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("The shift-closing response does not match the request.");
        return new(result.Id, result.BranchId, result.RegisterId, result.Currency, result.OpeningBalance,
            result.CashSales, result.CashRefunds, result.CashIn, result.CashOut, result.ExpectedCash,
            result.CountedCash, result.Variance, result.OpenedAt, result.ClosedAt, result.OpenedBy, result.ClosedBy);
    }

    private static void Validate(Guid organization, Guid branch, Guid shift, Guid operation)
    { if (organization == Guid.Empty || branch == Guid.Empty || shift == Guid.Empty || operation == Guid.Empty) throw new ArgumentException("Cash-management scope is required."); }
    private sealed record ClosedBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
        decimal OpeningBalance, decimal CashSales, decimal CashRefunds, decimal CashIn, decimal CashOut,
        decimal ExpectedCash, decimal CountedCash, decimal Variance, DateTimeOffset OpenedAt,
        DateTimeOffset ClosedAt, string OpenedBy, string ClosedBy);
    private sealed record OpenRequestBody(Guid RegisterId, string Currency, decimal OpeningBalance);
    private sealed record OpenResponseBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
        decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
    private static bool InvalidActor(string? value) => string.IsNullOrWhiteSpace(value) || value != value.Trim()
        || value.Length > 512 || value.Any(char.IsControl);
}
