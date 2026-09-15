namespace SalekhPos.Desktop.Application.Management;

public sealed record CustomerSummary(Guid Id, string Code, string DisplayName, string? Email,
    string? Phone, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SupplierSummary(Guid Id, string Code, string Name, string? TaxId, string? Email,
    string? Phone, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record EmployeeSummary(Guid Id, Guid BranchId, string Code, string DisplayName,
    string? Email, string? Phone, string JobTitle, bool IsActive, long Version,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderLineSummary(Guid ProductId, decimal Quantity, decimal UnitCost, decimal LineTotal);
public sealed record PurchaseOrderSummary(Guid Id, Guid BranchId, Guid SupplierId, string Status,
    string Currency, string? Reference, decimal Total, long Version, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<PurchaseOrderLineSummary> Lines);
public sealed record OperationalReportSummary(Guid OrganizationId, Guid BranchId, DateTimeOffset From,
    DateTimeOffset To, string? Currency, int SalesCount, decimal SalesGross, int ReturnCount,
    decimal ReturnsTotal, decimal NetSales, int PurchaseOrderCount, decimal PurchaseOrderTotal,
    int OpenShiftCount);
public sealed record UuidPage<T>(IReadOnlyList<T> Items, Guid? NextCursor);
public sealed record CreateCustomerInput(string Code, string DisplayName, string? Email, string? Phone);
public sealed record CreateSupplierInput(string Code, string Name, string? TaxId, string? Email, string? Phone);
public sealed record CreateEmployeeInput(string Code, string DisplayName, string? Email, string? Phone, string JobTitle);
public sealed record CreatePurchaseOrderLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record CreatePurchaseOrderInput(Guid SupplierId, string Currency, string? Reference,
    IReadOnlyList<CreatePurchaseOrderLineInput> Lines);

public interface IManagerBusiness
{
    Task<UuidPage<CustomerSummary>> ListCustomersAsync(Guid organizationId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<CustomerSummary> CreateCustomerAsync(Guid organizationId, CreateCustomerInput input, Guid operationId, CancellationToken cancellationToken);
    Task<UuidPage<SupplierSummary>> ListSuppliersAsync(Guid organizationId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<SupplierSummary> CreateSupplierAsync(Guid organizationId, CreateSupplierInput input, Guid operationId, CancellationToken cancellationToken);
    Task<UuidPage<EmployeeSummary>> ListEmployeesAsync(Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<EmployeeSummary> CreateEmployeeAsync(Guid organizationId, Guid branchId, CreateEmployeeInput input, Guid operationId, CancellationToken cancellationToken);
    Task<UuidPage<PurchaseOrderSummary>> ListPurchaseOrdersAsync(Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken);
    Task<PurchaseOrderSummary> CreatePurchaseOrderAsync(Guid organizationId, Guid branchId, CreatePurchaseOrderInput input, Guid operationId, CancellationToken cancellationToken);
    Task<PurchaseOrderSummary> ChangePurchaseOrderStatusAsync(Guid organizationId, Guid branchId, PurchaseOrderSummary order, string action, CancellationToken cancellationToken);
    Task<OperationalReportSummary> ReadOperationalReportAsync(Guid organizationId, Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
