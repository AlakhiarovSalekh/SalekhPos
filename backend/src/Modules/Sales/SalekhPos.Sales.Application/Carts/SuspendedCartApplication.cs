using SalekhPos.Sales.Contracts.Carts;

namespace SalekhPos.Sales.Application.Carts;

public sealed record SuspendCartCommand(Guid OrganizationId, Guid BranchId, Guid CartId, Guid OperationId,
    IReadOnlyList<SuspendCartLineRequest> Lines, string? Note);
public sealed record CartWriteResult(SuspendedCartResponse Cart, bool Created);
public interface ISuspendedCartService
{
    Task<CartWriteResult> SuspendAsync(string issuer, string subject, SuspendCartCommand command,
        CancellationToken cancellationToken);
    Task<SuspendedCartResponse?> ResumeAsync(string issuer, string subject, Guid organizationId, Guid branchId,
        Guid cartId, CancellationToken cancellationToken);
}
public sealed class CartDeniedException : Exception;
public sealed class CartConflictException : Exception;
public sealed class SalesCartUnavailableException : Exception;
