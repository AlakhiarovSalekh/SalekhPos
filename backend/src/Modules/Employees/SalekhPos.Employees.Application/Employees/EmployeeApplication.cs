using SalekhPos.Employees.Contracts.Employees;
using SalekhPos.Employees.Domain.Employees;

namespace SalekhPos.Employees.Application.Employees;

public sealed record EmployeeIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public EmployeeIdentity(string issuer, string subject)
    {
        Issuer = Validate(issuer, 2048); Subject = Validate(subject, 256);
    }
    private static string Validate(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Employee identity is invalid.");
        return value;
    }
}

public sealed record CreateEmployeeCommand(Guid OrganizationId, Guid BranchId, Guid EmployeeId,
    Guid OperationId, string Code, string DisplayName, string? Email, string? Phone, string JobTitle)
{
    public Employee ToEmployee() => new(OrganizationId, EmployeeId, Code, DisplayName, Email, Phone, JobTitle);
}
public sealed record UpdateEmployeeCommand(Guid OrganizationId, Guid BranchId, Guid EmployeeId,
    string DisplayName, string? Email, string? Phone, string JobTitle, bool IsActive, long ExpectedVersion);
public sealed record EmployeeWriteResult(EmployeeResponse Employee, bool Created);

public interface IEmployeeDirectory
{
    Task<EmployeeWriteResult> CreateAsync(EmployeeIdentity identity, CreateEmployeeCommand command,
        CancellationToken cancellationToken);
    Task<EmployeePage> ListAsync(EmployeeIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<EmployeeResponse?> ReadAsync(EmployeeIdentity identity, Guid organizationId, Guid branchId,
        Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeResponse> UpdateAsync(EmployeeIdentity identity, UpdateEmployeeCommand command,
        CancellationToken cancellationToken);
}

public sealed class EmployeeDeniedException : Exception;
public sealed class EmployeeUnavailableException : Exception;
public sealed class EmployeeConflictException : Exception;
public sealed class EmployeeNotFoundException : Exception;
