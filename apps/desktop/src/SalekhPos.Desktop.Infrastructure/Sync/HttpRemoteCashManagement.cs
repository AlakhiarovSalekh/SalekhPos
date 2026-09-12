using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Shifts;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteCashManagement(HttpClient client) : IRemoteCashManagement
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
}
