namespace SalekhPos.Payments.Contracts.Payments;

public sealed record PaymentResponse(Guid Id, Guid SaleId, Guid BranchId, string Method, string Status,
    string Currency, decimal Amount, decimal Tendered, decimal Change, DateTimeOffset CompletedAt);
public sealed record RefundResponse(Guid Id, Guid PaymentId, Guid BranchId, string SourceKind, Guid SourceId,
    string Method, string Status, string Currency, decimal Amount, DateTimeOffset CompletedAt);
