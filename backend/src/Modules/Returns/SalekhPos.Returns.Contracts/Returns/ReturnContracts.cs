namespace SalekhPos.Returns.Contracts.Returns;

public sealed record CompleteReturnRequest(Guid SaleId, string Reason);
public sealed record ReturnedLineResponse(int LineNumber, Guid ProductId, decimal Quantity, decimal Amount);
public sealed record CompletedReturnResponse(Guid Id, Guid SaleId, Guid BranchId, string Currency, decimal Amount,
    string Reason, DateTimeOffset CompletedAt, IReadOnlyList<ReturnedLineResponse> Lines);
