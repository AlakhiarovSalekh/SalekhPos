using SalekhPos.Desktop.Domain.Shifts;

namespace SalekhPos.Desktop.Application.Shifts;

public sealed record RemoteDeviceAssignment(Guid DeviceId, Guid BranchId, Guid RegisterId, string Status,
    int SyncProtocolVersion);
public sealed record RemoteOpenShift(Guid ShiftId, Guid BranchId, Guid RegisterId, string Status, string Currency,
    decimal OpeningBalance, DateTimeOffset OpenedAt);
public sealed record RemoteCashSessionSnapshot(RemoteDeviceAssignment Device, RemoteOpenShift? Shift,
    DateTimeOffset CapturedAt);

public interface IRemoteCashSessionSource
{
    Task<RemoteCashSessionSnapshot> DownloadAsync(Guid organizationId, Guid branchId, Guid deviceId,
        CancellationToken cancellationToken);
}

public interface ILocalCashSessionStore
{
    Task<LocalCashSession?> ApplyAsync(Guid organizationId, Guid branchId, Guid deviceId,
        RemoteCashSessionSnapshot snapshot, CancellationToken cancellationToken);
    Task<LocalCashSession?> ReadActiveAsync(Guid organizationId, Guid branchId, Guid deviceId,
        CancellationToken cancellationToken);
    Task ConfirmClosedAsync(Guid organizationId, Guid branchId, Guid deviceId, Guid shiftId,
        CancellationToken cancellationToken);
}

public sealed record CashMovementResult(Guid Id, Guid ShiftId, string Kind, string Currency, decimal Amount,
    string Reason, DateTimeOffset RecordedAt, string RecordedBy);
public sealed record OpenCashSessionRequest(Guid RegisterId, string Currency, decimal OpeningBalance,
    Guid OperationId);
public sealed record OpenCashSessionResult(Guid ShiftId, Guid BranchId, Guid RegisterId, string Status,
    string Currency, decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
public sealed record ClosedCashSessionResult(Guid ShiftId, Guid BranchId, Guid RegisterId, string Currency,
    decimal OpeningBalance, decimal CashSales, decimal CashRefunds, decimal CashIn, decimal CashOut,
    decimal ExpectedCash, decimal CountedCash, decimal Variance, DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt, string OpenedBy, string ClosedBy);
public interface IRemoteCashManagement
{
    Task<OpenCashSessionResult> OpenAsync(Guid organizationId, Guid branchId, Guid deviceId,
        OpenCashSessionRequest request, CancellationToken cancellationToken);
    Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid deviceId, Guid shiftId,
        Guid registerId, Guid operationId, string kind, decimal amount, string reason,
        CancellationToken cancellationToken);
    Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid deviceId, Guid shiftId,
        Guid registerId, Guid operationId, decimal countedCash, CancellationToken cancellationToken);
}

public sealed class CashSessionCoordinator(IRemoteCashSessionSource remote, ILocalCashSessionStore local)
{
    public async Task<LocalCashSession?> RefreshAsync(Guid organizationId, Guid branchId, Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty)
            throw new ArgumentException("Cash-session scope is required.");
        var snapshot = await remote.DownloadAsync(organizationId, branchId, deviceId, cancellationToken);
        return await local.ApplyAsync(organizationId, branchId, deviceId, snapshot, cancellationToken);
    }

    public async Task<LocalCashSession?> RefreshOpenedAsync(Guid organizationId, Guid branchId, Guid deviceId,
        OpenCashSessionResult expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty
            || expected.ShiftId == Guid.Empty || expected.BranchId != branchId || expected.RegisterId == Guid.Empty
            || expected.Status != "open" || expected.Currency is null || expected.Currency.Length != 3
            || expected.Currency.Any(c => c is < 'A' or > 'Z') || expected.OpeningBalance < 0
            || decimal.Round(expected.OpeningBalance, 6) != expected.OpeningBalance || expected.OpenedAt == default
            || expected.OpenedAt.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(expected.OpenedBy)
            || expected.OpenedBy != expected.OpenedBy.Trim() || expected.OpenedBy.Length > 512
            || expected.OpenedBy.Any(char.IsControl))
            throw new ArgumentException("Opened cash-session evidence is invalid.");
        var snapshot = await remote.DownloadAsync(organizationId, branchId, deviceId, cancellationToken);
        var shift = snapshot.Shift;
        if (snapshot.Device.DeviceId != deviceId || snapshot.Device.BranchId != branchId
            || snapshot.Device.RegisterId != expected.RegisterId || snapshot.Device.Status != "active"
            || snapshot.Device.SyncProtocolVersion != 1 || shift is null || shift.ShiftId != expected.ShiftId
            || shift.BranchId != branchId || shift.RegisterId != expected.RegisterId || shift.Status != "open"
            || shift.Currency != expected.Currency || shift.OpeningBalance != expected.OpeningBalance
            || shift.OpenedAt != expected.OpenedAt)
            throw new InvalidOperationException("The authoritative cash-session refresh does not match the opened shift.");
        return await local.ApplyAsync(organizationId, branchId, deviceId, snapshot, cancellationToken);
    }
}
