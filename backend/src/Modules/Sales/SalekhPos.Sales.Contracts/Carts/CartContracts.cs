namespace SalekhPos.Sales.Contracts.Carts;

public sealed record SuspendCartLineRequest(Guid ProductId, decimal Quantity);
public sealed record SuspendCartRequest(IReadOnlyList<SuspendCartLineRequest> Lines, string? Note);
public sealed record SuspendedCartLineResponse(int LineNumber, Guid ProductId, decimal Quantity);
public sealed record SuspendedCartResponse(Guid Id, Guid BranchId, string? Note, DateTimeOffset SuspendedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? ResumedAt, IReadOnlyList<SuspendedCartLineResponse> Lines);
