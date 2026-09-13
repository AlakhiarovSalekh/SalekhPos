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

    public async Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid deviceId,
        Guid shiftId, Guid registerId, Guid operationId, string kind, decimal amount, string reason,
        CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, deviceId, shiftId, registerId, operationId);
        if (kind is not ("cash_in" or "cash_out") || amount <= 0 || decimal.Round(amount, 6) != amount
            || InvalidReason(reason))
            throw new ArgumentException("Cash-movement request is invalid.");
        if (proofSigner is null)
            throw new InvalidOperationException("Trusted device request signing is not configured.");

        var path = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/cash-movements";
        var body = JsonSerializer.SerializeToUtf8Bytes(new CashMovementRequestBody(registerId, kind, amount, reason),
            JsonOptions);
        var proof = await proofSigner.SignCashMovementAsync(organizationId, branchId, deviceId, shiftId,
            operationId, path, body, cancellationToken);
        using var request = CreateSignedRequest(path, operationId, deviceId, proof, body);
        using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CashMovementResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The cash-movement response is empty.");
        if (result.Id == Guid.Empty || result.ShiftId != shiftId || result.Kind != kind || result.Amount != amount
            || result.Reason != reason || InvalidCurrency(result.Currency) || result.RecordedAt == default
            || result.RecordedAt.Offset != TimeSpan.Zero || InvalidActor(result.RecordedBy))
            throw new InvalidOperationException("The cash-movement response does not match the request.");
        return result;
    }

    public async Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid deviceId,
        Guid shiftId, Guid registerId, Guid operationId, decimal countedCash,
        CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, deviceId, shiftId, registerId, operationId);
        if (countedCash < 0 || decimal.Round(countedCash, 6) != countedCash)
            throw new ArgumentException("Shift-closing request is invalid.");
        if (proofSigner is null)
            throw new InvalidOperationException("Trusted device request signing is not configured.");

        var path = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/{shiftId:D}/close";
        var body = JsonSerializer.SerializeToUtf8Bytes(new ShiftCloseRequestBody(registerId, countedCash), JsonOptions);
        var proof = await proofSigner.SignShiftCloseAsync(organizationId, branchId, deviceId, shiftId, operationId,
            path, body, cancellationToken);
        using var request = CreateSignedRequest(path, operationId, deviceId, proof, body);
        using var response = await client.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ClosedBody>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The shift-closing response is empty.");
        if (result.Id != shiftId || result.BranchId != branchId || result.RegisterId != registerId
            || result.Status != "closed" || InvalidCurrency(result.Currency) || result.CountedCash != countedCash
            || InvalidUnsignedAmount(result.OpeningBalance) || InvalidUnsignedAmount(result.CashSales)
            || InvalidUnsignedAmount(result.CashRefunds) || InvalidUnsignedAmount(result.CashIn)
            || InvalidUnsignedAmount(result.CashOut) || InvalidAmount(result.ExpectedCash)
            || InvalidAmount(result.Variance) || !HasValidReconciliation(result) || result.OpenedAt == default
            || result.OpenedAt.Offset != TimeSpan.Zero || result.ClosedAt == default
            || result.ClosedAt.Offset != TimeSpan.Zero || result.ClosedAt < result.OpenedAt
            || InvalidActor(result.OpenedBy) || InvalidActor(result.ClosedBy))
            throw new InvalidOperationException("The shift-closing response does not match the request.");
        return new(result.Id, result.BranchId, result.RegisterId, result.Currency, result.OpeningBalance,
            result.CashSales, result.CashRefunds, result.CashIn, result.CashOut, result.ExpectedCash,
            result.CountedCash, result.Variance, result.OpenedAt, result.ClosedAt, result.OpenedBy, result.ClosedBy);
    }

    private static HttpRequestMessage CreateSignedRequest(string path, Guid operationId, Guid deviceId,
        DeviceRequestProof proof, byte[] body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        request.Headers.Add("X-SalekhPos-Device-Id", deviceId.ToString("D"));
        request.Headers.Add("X-SalekhPos-Device-Credential", proof.CredentialId);
        request.Headers.Add("X-SalekhPos-Device-Timestamp", proof.Timestamp);
        request.Headers.Add("X-SalekhPos-Device-Nonce", proof.Nonce);
        request.Headers.Add("X-SalekhPos-Device-Signature", proof.Signature);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        { CharSet = Encoding.UTF8.WebName };
        return request;
    }
    private static void Validate(Guid organization, Guid branch, Guid device, Guid shift, Guid register,
        Guid operation)
    {
        if (organization == Guid.Empty || branch == Guid.Empty || device == Guid.Empty || shift == Guid.Empty
            || register == Guid.Empty || operation == Guid.Empty)
            throw new ArgumentException("Cash-management scope is required.");
    }
    private static bool InvalidReason(string? value) => string.IsNullOrWhiteSpace(value) || value != value.Trim()
        || value.Length is < 3 or > 500 || value.Any(char.IsControl);
    private static bool InvalidCurrency(string? value) => value is null || value.Length != 3
        || value.Any(c => c is < 'A' or > 'Z');
    private static bool InvalidAmount(decimal value) => decimal.Round(value, 6) != value;
    private static bool InvalidUnsignedAmount(decimal value) => value < 0 || InvalidAmount(value);
    private static bool HasValidReconciliation(ClosedBody result)
    {
        try
        {
            return result.ExpectedCash == checked(result.OpeningBalance + result.CashSales - result.CashRefunds
                + result.CashIn - result.CashOut)
                && result.Variance == checked(result.CountedCash - result.ExpectedCash);
        }
        catch (OverflowException)
        {
            return false;
        }
    }
    private sealed record ClosedBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
        decimal OpeningBalance, decimal CashSales, decimal CashRefunds, decimal CashIn, decimal CashOut,
        decimal ExpectedCash, decimal CountedCash, decimal Variance, DateTimeOffset OpenedAt,
        DateTimeOffset ClosedAt, string OpenedBy, string ClosedBy);
    private sealed record OpenRequestBody(Guid RegisterId, string Currency, decimal OpeningBalance);
    private sealed record CashMovementRequestBody(Guid RegisterId, string Kind, decimal Amount, string Reason);
    private sealed record ShiftCloseRequestBody(Guid RegisterId, decimal CountedCash);
    private sealed record OpenResponseBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
        decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
    private static bool InvalidActor(string? value) => string.IsNullOrWhiteSpace(value) || value != value.Trim()
        || value.Length > 512 || value.Any(char.IsControl);
}
