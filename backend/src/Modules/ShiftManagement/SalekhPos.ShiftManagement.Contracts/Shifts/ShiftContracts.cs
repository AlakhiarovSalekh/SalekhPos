namespace SalekhPos.ShiftManagement.Contracts.Shifts;

public sealed record OpenShiftRequest(Guid RegisterId, string Currency, decimal OpeningBalance);
public sealed record ShiftResponse(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency, decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
public sealed record RecordCashMovementRequest(string Kind, decimal Amount, string Reason);
public sealed record CashMovementResponse(Guid Id, Guid ShiftId, string Kind, string Currency, decimal Amount, string Reason, DateTimeOffset RecordedAt, string RecordedBy);
