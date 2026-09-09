using SalekhPos.Returns.Contracts.Returns;

namespace SalekhPos.Returns.Application.Returns;

public sealed record ReturnIdentity(string Issuer, string Subject);
public sealed record CompleteReturnCommand(Guid OrganizationId, Guid BranchId, Guid ReturnId, Guid OperationId,
    Guid SaleId, string Reason);
public sealed record ReturnWriteResult(CompletedReturnResponse Return, bool Created);
public interface IReturnCompletion
{
    Task<ReturnWriteResult> CompleteAsync(ReturnIdentity identity, CompleteReturnCommand command,
        CancellationToken cancellationToken);
}
public sealed class ReturnDeniedException : Exception;
public sealed class ReturnConflictException : Exception;
public sealed class ReturnSaleNotFoundException : Exception;
public sealed class ReturnsUnavailableException : Exception;
