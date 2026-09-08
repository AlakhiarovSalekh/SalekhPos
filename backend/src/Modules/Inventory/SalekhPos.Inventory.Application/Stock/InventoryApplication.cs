using SalekhPos.Inventory.Contracts.Stock;
using SalekhPos.Inventory.Domain.StockMovements;

namespace SalekhPos.Inventory.Application.Stock;

public sealed record InventoryIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public InventoryIdentity(string issuer, string subject)
    {
        Issuer = Valid(issuer, 2048, nameof(issuer)); Subject = Valid(subject, 256, nameof(subject));
    }
    private static string Valid(string value, int maximum, string parameter) =>
        string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)
            ? throw new ArgumentException("Identity is invalid.", parameter) : value;
}
public sealed record CreateStockMovementCommand(Guid OrganizationId, Guid BranchId, Guid ProductId,
    Guid MovementId, Guid OperationId, StockMovementKind Kind, decimal Quantity, string? Reason,
    DateTimeOffset OccurredAt)
{
    public StockMovement ToMovement() => new(OrganizationId, BranchId, ProductId, MovementId,
        Kind, Quantity, Reason, OccurredAt);
}
public sealed record StockMovementWriteResult(StockMovementResponse Movement, bool Created);
public interface IInventoryLedger
{
    Task<StockMovementWriteResult> RecordAsync(InventoryIdentity identity, CreateStockMovementCommand command,
        CancellationToken cancellationToken);
    Task<StockPage> ReadStockAsync(InventoryIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
}
public sealed class InventoryDeniedException : Exception;
public sealed class InventoryConflictException : Exception;
public sealed class InventoryUnavailableException : Exception;
