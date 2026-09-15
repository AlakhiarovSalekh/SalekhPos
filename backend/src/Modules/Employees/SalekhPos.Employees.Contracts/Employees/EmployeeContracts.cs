namespace SalekhPos.Employees.Contracts.Employees;

public sealed record CreateEmployeeRequest(string Code, string DisplayName, string? Email,
    string? Phone, string JobTitle);
public sealed record UpdateEmployeeRequest(string DisplayName, string? Email, string? Phone,
    string JobTitle, bool IsActive, long ExpectedVersion);
public sealed record EmployeeResponse(Guid Id, Guid BranchId, string Code, string DisplayName,
    string? Email, string? Phone, string JobTitle, bool IsActive, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record EmployeePage(IReadOnlyList<EmployeeResponse> Items, Guid? NextCursor);
