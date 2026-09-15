using SalekhPos.Customers.Contracts.Customers;
using SalekhPos.Customers.Domain.Customers;

namespace SalekhPos.Customers.Application.Customers;

public sealed record CustomerIdentity
{
    public string Issuer { get; }
    public string Subject { get; }

    public CustomerIdentity(string issuer, string subject)
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

public sealed record CreateCustomerCommand(Guid OrganizationId, Guid CustomerId, Guid OperationId,
    string Code, string DisplayName, string? Email, string? Phone)
{
    public Customer ToCustomer() => new(OrganizationId, CustomerId, Code, DisplayName, Email, Phone);
}

public sealed record UpdateCustomerCommand(Guid OrganizationId, Guid CustomerId, string DisplayName,
    string? Email, string? Phone, bool IsActive, long ExpectedVersion);
public sealed record CustomerWriteResult(CustomerResponse Customer, bool Created);

public interface ICustomerDirectory
{
    Task<CustomerWriteResult> CreateAsync(CustomerIdentity identity, CreateCustomerCommand command,
        CancellationToken cancellationToken);
    Task<CustomerPage> ListAsync(CustomerIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
    Task<CustomerResponse?> ReadAsync(CustomerIdentity identity, Guid organizationId, Guid customerId,
        CancellationToken cancellationToken);
    Task<CustomerResponse> UpdateAsync(CustomerIdentity identity, UpdateCustomerCommand command,
        CancellationToken cancellationToken);
}

public sealed class CustomerDeniedException : Exception;
public sealed class CustomerUnavailableException : Exception;
public sealed class CustomerConflictException : Exception;
public sealed class CustomerNotFoundException : Exception;
