using SalekhPos.ShiftManagement.Contracts.Shifts;
namespace SalekhPos.ShiftManagement.Application.Shifts;

public sealed record ShiftIdentity(string Issuer, string Subject);
public sealed record ShiftDeviceRequestProofHeaders(string? CredentialId, string? Timestamp, string? Nonce, string? Signature);
public sealed record ShiftDeviceRequestProofContext(ShiftIdentity Identity, Guid OrganizationId, Guid BranchId, Guid DeviceId,
    Guid RegisterId, string Method, string CanonicalPath, string OperationIdentity, string BodyDigest,
    ShiftDeviceRequestProofHeaders Headers);
public interface IShiftDeviceRequestAuthorizer
{
    Task VerifyAsync(ShiftDeviceRequestProofContext context, CancellationToken cancellationToken);
}
public sealed record OpenShiftCommand(Guid OrganizationId, Guid BranchId, Guid ShiftId, Guid OperationId, Guid RegisterId, string Currency, decimal OpeningBalance);
public sealed record ShiftWriteResult(ShiftResponse Shift, bool Created);
public sealed record RecordCashMovementCommand(Guid OrganizationId, Guid BranchId, Guid ShiftId, Guid MovementId,
    Guid OperationId, Guid RegisterId, string Kind, decimal Amount, string Reason);
public sealed record CashMovementWriteResult(CashMovementResponse Movement, bool Created);
public sealed record CloseShiftCommand(Guid OrganizationId, Guid BranchId, Guid ShiftId, Guid OperationId,
    Guid RegisterId, decimal CountedCash);
public sealed record CloseShiftWriteResult(ClosedShiftResponse Shift, bool Created);
public interface IShiftService { Task<ShiftWriteResult> OpenAsync(ShiftIdentity identity, OpenShiftCommand command, CancellationToken cancellationToken); Task<ShiftResponse?> ReadOpenAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid registerId, CancellationToken cancellationToken); Task<CashMovementWriteResult> RecordCashMovementAsync(ShiftIdentity identity, RecordCashMovementCommand command, CancellationToken cancellationToken); Task<IReadOnlyList<CashMovementResponse>> ListCashMovementsAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken); Task<CloseShiftWriteResult> CloseAsync(ShiftIdentity identity, CloseShiftCommand command, CancellationToken cancellationToken); Task<ClosedShiftResponse?> ReadClosedAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken); Task<ClosedShiftPage> ListClosedAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken); }
public sealed class ShiftDeniedException : Exception; public sealed class ShiftRequestAuthenticationException : Exception; public sealed class ShiftConflictException : Exception; public sealed class ShiftUnavailableException : Exception;
