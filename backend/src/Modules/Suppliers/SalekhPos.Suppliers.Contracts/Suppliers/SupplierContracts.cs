namespace SalekhPos.Suppliers.Contracts.Suppliers;

public sealed record CreateSupplierRequest(string Code, string Name, string? TaxId, string? Email, string? Phone);
public sealed record UpdateSupplierRequest(string Name, string? TaxId, string? Email, string? Phone,
    bool IsActive, long ExpectedVersion);
public sealed record SupplierResponse(Guid Id, string Code, string Name, string? TaxId, string? Email,
    string? Phone, bool IsActive, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SupplierPage(IReadOnlyList<SupplierResponse> Items, Guid? NextCursor);
