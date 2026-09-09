namespace SalekhPos.ShiftManagement.Contracts.Shifts;

public sealed record OpenShiftRequest(Guid RegisterId, string Currency, decimal OpeningBalance);
public sealed record ShiftResponse(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency, decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
