using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Shifts;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteCashSessionSource(HttpClient client) : IRemoteCashSessionSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RemoteCashSessionSnapshot> DownloadAsync(Guid organizationId, Guid branchId, Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty)
            throw new ArgumentException("Cash-session scope is required.");
        var device = await client.GetFromJsonAsync<DeviceBody>(
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{deviceId:D}", JsonOptions,
            cancellationToken) ?? throw new InvalidOperationException("The device assignment body is empty.");
        if (device.Id != deviceId || device.BranchId != branchId || device.RegisterId == Guid.Empty
            || device.Status != "active" || device.SyncProtocolVersion != 1)
            throw new InvalidOperationException("The device assignment is not eligible for offline sales.");

        using var response = await client.GetAsync(
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/shifts/open?registerId={device.RegisterId:D}",
            cancellationToken);
        ShiftBody? shift = null;
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
            shift = await response.Content.ReadFromJsonAsync<ShiftBody>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("The open-shift body is empty.");
            if (shift.Id == Guid.Empty || shift.BranchId != branchId || shift.RegisterId != device.RegisterId
                || shift.Status != "open" || shift.Currency is null || shift.OpenedAt.Offset != TimeSpan.Zero)
                throw new InvalidOperationException("The open-shift assignment is invalid.");
        }
        var capturedAt = DateTimeOffset.UtcNow;
        return new(new(device.Id, device.BranchId, device.RegisterId, device.Status, device.SyncProtocolVersion),
            shift is null ? null : new(shift.Id, shift.BranchId, shift.RegisterId, shift.Status, shift.Currency,
                shift.OpeningBalance, shift.OpenedAt), capturedAt);
    }

    private sealed record DeviceBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, int SyncProtocolVersion);
    private sealed record ShiftBody(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
        decimal OpeningBalance, DateTimeOffset OpenedAt);
}
