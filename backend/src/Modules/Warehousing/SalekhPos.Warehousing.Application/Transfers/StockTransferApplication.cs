using SalekhPos.Warehousing.Contracts.Transfers;
using SalekhPos.Warehousing.Domain.Dispatch;

namespace SalekhPos.Warehousing.Application.Transfers;

public sealed record WarehousingIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Warehousing identity is invalid.");
    }
}

public sealed record CreateStockTransferCommand(Guid OrganizationId, Guid TransferId, Guid OperationId,
    Guid SourceBranchId, Guid DestinationBranchId, string? Reference,
    IReadOnlyList<CreateStockTransferLineRequest> Lines)
{
    public StockTransfer ToTransfer() => new(OrganizationId, TransferId, SourceBranchId, DestinationBranchId,
        Reference, Lines.Select(line => new StockTransferLine(line.ProductId, line.Quantity)));
}

public sealed record StockTransferWriteResult(StockTransferResponse Transfer, bool Created);

public interface IStockTransferService
{
    Task<StockTransferWriteResult> CreateAsync(WarehousingIdentity identity, CreateStockTransferCommand command,
        CancellationToken cancellationToken);
    Task<StockTransferPage> ListAsync(WarehousingIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<StockTransferResponse?> ReadAsync(WarehousingIdentity identity, Guid organizationId, Guid branchId,
        Guid transferId, CancellationToken cancellationToken);
    Task<StockTransferResponse> DispatchAsync(WarehousingIdentity identity, Guid organizationId,
        Guid sourceBranchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken);
    Task<StockTransferResponse> ReceiveAsync(WarehousingIdentity identity, Guid organizationId,
        Guid destinationBranchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken);
    Task<StockTransferResponse> CancelAsync(WarehousingIdentity identity, Guid organizationId,
        Guid branchId, Guid transferId, long expectedVersion, CancellationToken cancellationToken);
}

public sealed class WarehousingDeniedException : Exception;
public sealed class WarehousingUnavailableException : Exception;
public sealed class StockTransferConflictException : Exception;
public sealed class StockTransferNotFoundException : Exception;
public sealed class StockTransferInsufficientStockException : Exception;
