using SalekhPos.Suppliers.Contracts.Suppliers;
using SalekhPos.Suppliers.Domain.Suppliers;

namespace SalekhPos.Suppliers.Application.Suppliers;

public sealed record SupplierIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public SupplierIdentity(string issuer, string subject)
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

public sealed record CreateSupplierCommand(Guid OrganizationId, Guid SupplierId, Guid OperationId,
    string Code, string Name, string? TaxId, string? Email, string? Phone)
{
    public Supplier ToSupplier() => new(OrganizationId, SupplierId, Code, Name, TaxId, Email, Phone);
}

public sealed record UpdateSupplierCommand(Guid OrganizationId, Guid SupplierId, string Name, string? TaxId,
    string? Email, string? Phone, bool IsActive, long ExpectedVersion);
public sealed record SupplierWriteResult(SupplierResponse Supplier, bool Created);

public interface ISupplierDirectory
{
    Task<SupplierWriteResult> CreateAsync(SupplierIdentity identity, CreateSupplierCommand command,
        CancellationToken cancellationToken);
    Task<SupplierPage> ListAsync(SupplierIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
    Task<SupplierResponse?> ReadAsync(SupplierIdentity identity, Guid organizationId, Guid supplierId,
        CancellationToken cancellationToken);
    Task<SupplierResponse> UpdateAsync(SupplierIdentity identity, UpdateSupplierCommand command,
        CancellationToken cancellationToken);
}

public sealed class SupplierDeniedException : Exception;
public sealed class SupplierUnavailableException : Exception;
public sealed class SupplierConflictException : Exception;
public sealed class SupplierNotFoundException : Exception;
