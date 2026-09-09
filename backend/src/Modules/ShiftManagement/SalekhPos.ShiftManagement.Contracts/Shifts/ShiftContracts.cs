namespace SalekhPos.ShiftManagement.Contracts.Shifts;

public sealed record OpenShiftRequest(Guid RegisterId, string Currency, decimal OpeningBalance);
public sealed record ShiftResponse(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency, decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
public sealed record RecordCashMovementRequest(string Kind, decimal Amount, string Reason);
public sealed record CashMovementResponse(Guid Id, Guid ShiftId, string Kind, string Currency, decimal Amount, string Reason, DateTimeOffset RecordedAt, string RecordedBy);
public sealed record CloseShiftRequest(decimal CountedCash);
public sealed record ClosedShiftResponse(Guid Id, Guid BranchId, Guid RegisterId, string Status, string Currency,
    decimal OpeningBalance, decimal CashSales, decimal CashRefunds, decimal CashIn, decimal CashOut,
    decimal ExpectedCash, decimal CountedCash, decimal Variance, DateTimeOffset OpenedAt, DateTimeOffset ClosedAt,
    string OpenedBy, string ClosedBy);
public sealed record ClosedShiftPage(IReadOnlyList<ClosedShiftResponse> Items, Guid? NextCursor);
