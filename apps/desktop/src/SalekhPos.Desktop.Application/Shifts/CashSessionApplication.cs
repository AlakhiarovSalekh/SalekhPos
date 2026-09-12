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
public sealed record ClosedCashSessionResult(Guid ShiftId, Guid BranchId, Guid RegisterId, string Currency,
    decimal OpeningBalance, decimal CashSales, decimal CashRefunds, decimal CashIn, decimal CashOut,
    decimal ExpectedCash, decimal CountedCash, decimal Variance, DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt, string OpenedBy, string ClosedBy);
public interface IRemoteCashManagement
{
    Task<CashMovementResult> RecordMovementAsync(Guid organizationId, Guid branchId, Guid shiftId,
        Guid operationId, string kind, decimal amount, string reason, CancellationToken cancellationToken);
    Task<ClosedCashSessionResult> CloseAsync(Guid organizationId, Guid branchId, Guid shiftId,
        Guid operationId, decimal countedCash, CancellationToken cancellationToken);
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
}
