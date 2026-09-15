namespace SalekhPos.Desktop.Application.Operations;

public sealed record RegisterSummary(Guid Id, Guid BranchId, string Code, string Name,
    bool IsActive, DateTimeOffset CreatedAt);
public sealed record RegisterPage(IReadOnlyList<RegisterSummary> Items, Guid? NextCursor);
public sealed record ResolvedPrice(Guid PriceId, Guid ProductId, Guid? BranchId, decimal Amount,
    string Currency, string TaxMode, decimal TaxRate, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);
public sealed record OpenShiftSummary(Guid Id, Guid BranchId, Guid RegisterId, string Status,
    string Currency, decimal OpeningBalance, DateTimeOffset OpenedAt, string OpenedBy);
public sealed record CashMovementSummary(Guid Id, Guid ShiftId, string Kind, string Currency,
    decimal Amount, string Reason, DateTimeOffset RecordedAt, string RecordedBy);
public sealed record ClosedShiftSummary(Guid Id, Guid BranchId, Guid RegisterId, string Status,
    string Currency, decimal OpeningBalance, decimal CashSales, decimal CashRefunds,
    decimal CashIn, decimal CashOut, decimal ExpectedCash, decimal CountedCash, decimal Variance,
    DateTimeOffset OpenedAt, DateTimeOffset ClosedAt, string OpenedBy, string ClosedBy);
public sealed record ClosedShiftPage(IReadOnlyList<ClosedShiftSummary> Items, Guid? NextCursor);
public sealed record PaymentEventSummary(Guid Id, Guid PaymentId, Guid BranchId, string Kind,
    Guid SourceId, string Method, string Status, string Currency, decimal Amount, DateTimeOffset CompletedAt);
public sealed record PaymentEventPage(IReadOnlyList<PaymentEventSummary> Items, string? NextCursor);

public interface IManagerOperations
{
    Task<RegisterPage> ListRegistersAsync(Guid organizationId, Guid branchId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
    Task<ResolvedPrice?> ResolvePriceAsync(Guid organizationId, Guid branchId, Guid productId,
        DateTimeOffset at, CancellationToken cancellationToken);
    Task<OpenShiftSummary?> ReadOpenShiftAsync(Guid organizationId, Guid branchId, Guid registerId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CashMovementSummary>> ListCashMovementsAsync(Guid organizationId, Guid branchId,
        Guid shiftId, CancellationToken cancellationToken);
    Task<ClosedShiftPage> ListClosedShiftsAsync(Guid organizationId, Guid branchId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
    Task<ClosedShiftSummary?> ReadClosedShiftAsync(Guid organizationId, Guid branchId, Guid shiftId,
        CancellationToken cancellationToken);
    Task<PaymentEventPage> ListPaymentEventsAsync(Guid organizationId, Guid branchId, int pageSize,
        string? after, CancellationToken cancellationToken);
}
