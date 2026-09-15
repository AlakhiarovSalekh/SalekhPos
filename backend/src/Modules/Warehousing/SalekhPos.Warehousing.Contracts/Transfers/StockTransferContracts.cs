namespace SalekhPos.Warehousing.Contracts.Transfers;

public sealed record CreateStockTransferLineRequest(Guid ProductId, decimal Quantity);
public sealed record CreateStockTransferRequest(Guid DestinationBranchId, string? Reference,
    IReadOnlyList<CreateStockTransferLineRequest> Lines);
public sealed record ChangeStockTransferStatusRequest(long ExpectedVersion);
public sealed record StockTransferLineResponse(Guid ProductId, decimal Quantity);
public sealed record StockTransferResponse(Guid Id, Guid SourceBranchId, Guid DestinationBranchId,
    string Status, string? Reference, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? DispatchedAt, DateTimeOffset? ReceivedAt, IReadOnlyList<StockTransferLineResponse> Lines);
public sealed record StockTransferPage(IReadOnlyList<StockTransferResponse> Items, Guid? NextCursor);
