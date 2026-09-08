namespace SalekhPos.Catalog.Contracts.Products;

public sealed record CreateProductRequest(string Sku, string Name, string UnitCode, string? Barcode);
public sealed record ProductResponse(Guid Id, string Sku, string Name, string UnitCode,
    string? Barcode, bool IsActive, long Version);
public sealed record ProductPage(IReadOnlyList<ProductResponse> Items, Guid? NextCursor);
