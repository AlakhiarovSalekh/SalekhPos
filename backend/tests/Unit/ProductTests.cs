using SalekhPos.Catalog.Domain.Products;
using Xunit;

namespace SalekhPos.Tests;

public sealed class ProductTests
{
    [Fact]
    public void ProductPreservesTenantIdentityAndNormalData()
    {
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "COFFEE.001", "Coffee 500 g", "EA", "12345678");
        var renamed = product.Rename("Coffee 1 kg");
        Assert.Equal(product.OrganizationId, renamed.OrganizationId);
        Assert.Equal(product.Id, renamed.Id);
        Assert.Equal("Coffee 1 kg", renamed.Name);
        Assert.False(renamed.Deactivate().IsActive);
    }

    [Theory]
    [InlineData("lower")]
    [InlineData("SPACE SKU")]
    [InlineData("")]
    public void InvalidSkusAreRejected(string sku) =>
        Assert.Throws<ArgumentException>(() => new Product(Guid.NewGuid(), Guid.NewGuid(), sku, "Product", "EA"));

    [Theory]
    [InlineData("123")]
    [InlineData("12A45")]
    [InlineData(" 1234")]
    public void InvalidBarcodesAreRejected(string barcode) =>
        Assert.Throws<ArgumentException>(() => new Product(Guid.NewGuid(), Guid.NewGuid(), "SKU", "Product", "EA", barcode));
}
