namespace SalekhPos.Customers.Contracts.Customers;

public sealed record CreateCustomerRequest(string Code, string DisplayName, string? Email, string? Phone);
public sealed record UpdateCustomerRequest(string DisplayName, string? Email, string? Phone,
    bool IsActive, long ExpectedVersion);
public sealed record CustomerResponse(Guid Id, string Code, string DisplayName, string? Email,
    string? Phone, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CustomerPage(IReadOnlyList<CustomerResponse> Items, Guid? NextCursor);
