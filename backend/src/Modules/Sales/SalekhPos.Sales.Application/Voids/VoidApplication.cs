using SalekhPos.Sales.Contracts.Voids;

namespace SalekhPos.Sales.Application.Voids;

public sealed record VoidSaleCommand(Guid OrganizationId, Guid BranchId, Guid VoidId, Guid OperationId,
    Guid SaleId, string Reason);
public sealed record VoidWriteResult(VoidedSaleResponse Void, bool Created);
public interface ISaleVoidService
{
    Task<VoidWriteResult> VoidAsync(string issuer, string subject, VoidSaleCommand command,
        CancellationToken cancellationToken);
}
public sealed class SaleVoidDeniedException : Exception;
public sealed class SaleVoidConflictException : Exception;
public sealed class SaleVoidNotFoundException : Exception;
public sealed class SaleVoidUnavailableException : Exception;
