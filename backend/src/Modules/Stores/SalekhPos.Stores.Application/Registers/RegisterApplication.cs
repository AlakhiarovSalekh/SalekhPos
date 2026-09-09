using SalekhPos.Stores.Contracts.Registers;
namespace SalekhPos.Stores.Application.Registers;

public sealed record StoreIdentity(string Issuer, string Subject);
public sealed record CreateRegisterCommand(Guid OrganizationId, Guid BranchId, Guid RegisterId, Guid OperationId, string Code, string Name);
public sealed record RegisterWriteResult(RegisterResponse Register, bool Created);
public interface IRegisterCatalog
{
    Task<RegisterWriteResult> CreateAsync(StoreIdentity identity, CreateRegisterCommand command, CancellationToken cancellationToken);
    Task<RegisterPage> ListAsync(StoreIdentity identity, Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken);
}
public sealed class StoreDeniedException : Exception;
public sealed class StoreConflictException : Exception;
public sealed class StoreUnavailableException : Exception;
