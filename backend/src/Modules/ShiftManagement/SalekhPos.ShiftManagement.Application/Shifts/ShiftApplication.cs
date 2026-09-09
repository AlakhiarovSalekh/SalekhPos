using SalekhPos.ShiftManagement.Contracts.Shifts;
namespace SalekhPos.ShiftManagement.Application.Shifts;

public sealed record ShiftIdentity(string Issuer, string Subject);
public sealed record OpenShiftCommand(Guid OrganizationId, Guid BranchId, Guid ShiftId, Guid OperationId, Guid RegisterId, string Currency, decimal OpeningBalance);
public sealed record ShiftWriteResult(ShiftResponse Shift, bool Created);
public interface IShiftService { Task<ShiftWriteResult> OpenAsync(ShiftIdentity identity, OpenShiftCommand command, CancellationToken cancellationToken); Task<ShiftResponse?> ReadOpenAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid registerId, CancellationToken cancellationToken); }
public sealed class ShiftDeniedException : Exception; public sealed class ShiftConflictException : Exception; public sealed class ShiftUnavailableException : Exception;
