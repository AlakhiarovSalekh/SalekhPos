using SalekhPos.Catalog.Contracts.Products;
using SalekhPos.Catalog.Domain.Products;

namespace SalekhPos.Catalog.Application.Products;

public sealed record CatalogIdentity
{
    public string Issuer { get; }
    public string Subject { get; }

    public CatalogIdentity(string issuer, string subject)
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

public sealed record CreateProductCommand(Guid OrganizationId, Guid ProductId, Guid OperationId,
    string Sku, string Name, string UnitCode, string? Barcode)
{
    public Product ToProduct() => new(OrganizationId, ProductId, Sku, Name, UnitCode, Barcode);
}

public sealed record ProductWriteResult(ProductResponse Product, bool Created);
public sealed record UpdateProductCommand(Guid OrganizationId, Guid ProductId, string Name,
    string UnitCode, string? Barcode, bool IsActive, long ExpectedVersion);

public interface IProductCatalog
{
    Task<ProductWriteResult> CreateAsync(CatalogIdentity identity, CreateProductCommand command,
        CancellationToken cancellationToken);
    Task<ProductPage> ReadAsync(CatalogIdentity identity, Guid organizationId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
    Task<ProductResponse?> ReadOneAsync(CatalogIdentity identity, Guid organizationId,
        Guid? productId, string? barcode, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateAsync(CatalogIdentity identity, UpdateProductCommand command,
        CancellationToken cancellationToken);
}

public sealed class CatalogDeniedException : Exception;
public sealed class CatalogUnavailableException : Exception;
public sealed class ProductConflictException : Exception;
public sealed class ProductNotFoundException : Exception;
