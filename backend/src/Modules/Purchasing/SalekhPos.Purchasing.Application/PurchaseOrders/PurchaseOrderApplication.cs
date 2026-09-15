using SalekhPos.Purchasing.Contracts.PurchaseOrders;
using SalekhPos.Purchasing.Domain.PurchaseOrders;

namespace SalekhPos.Purchasing.Application.PurchaseOrders;

public sealed record PurchasingIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public PurchasingIdentity(string issuer, string subject)
    {
        Issuer = Validate(issuer, 2048, nameof(issuer));
        Subject = Validate(subject, 256, nameof(subject));
    }
    private static string Validate(string value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Identity value is invalid.", parameter);
        return value;
    }
}

public sealed record CreatePurchaseOrderLine(Guid ProductId, decimal Quantity, decimal UnitCost)
{
    public PurchaseOrderLine ToDomain() => new(ProductId, Quantity, UnitCost);
}
public sealed record CreatePurchaseOrderCommand(Guid OrganizationId, Guid OrderId, Guid OperationId,
    Guid BranchId, Guid SupplierId, string Currency, string? Reference,
    IReadOnlyList<CreatePurchaseOrderLine> Lines)
{
    public PurchaseOrder ToOrder() => new(OrganizationId, OrderId, BranchId, SupplierId, Currency,
        Reference, Lines.Select(line => line.ToDomain()));
}

public sealed record ChangePurchaseOrderStatusCommand(Guid OrganizationId, Guid BranchId,
    Guid OrderId, string TargetStatus, long ExpectedVersion);
public sealed record PurchaseOrderWriteResult(PurchaseOrderResponse Order, bool Created);

public interface IPurchaseOrderService
{
    Task<PurchaseOrderWriteResult> CreateAsync(PurchasingIdentity identity,
        CreatePurchaseOrderCommand command, CancellationToken cancellationToken);
    Task<PurchaseOrderPage> ListAsync(PurchasingIdentity identity, Guid organizationId,
        Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<PurchaseOrderResponse?> ReadAsync(PurchasingIdentity identity, Guid organizationId,
        Guid branchId, Guid orderId, CancellationToken cancellationToken);
    Task<PurchaseOrderResponse> ChangeStatusAsync(PurchasingIdentity identity,
        ChangePurchaseOrderStatusCommand command, CancellationToken cancellationToken);
}

public sealed class PurchasingDeniedException : Exception;
public sealed class PurchasingUnavailableException : Exception;
public sealed class PurchaseOrderConflictException : Exception;
public sealed class PurchaseOrderNotFoundException : Exception;
