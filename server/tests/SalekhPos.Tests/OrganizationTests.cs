using SalekhPos.Organizations;
using Xunit;

namespace SalekhPos.Tests;

public sealed class OrganizationTests
{
    [Theory]
    [InlineData("თბილისის მაღაზია")]
    [InlineData("Şəki mağazası")]
    [InlineData("متجر")]
    public void NamesPreserveUserLanguage(string name)
    {
        Assert.Equal(name, new Organization(Guid.NewGuid(), name).Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("line\nbreak")]
    public void InvalidNamesAreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new Organization(Guid.NewGuid(), name));
    }

    [Fact]
    public void NameLimitCountsUnicodeCharactersInsteadOfUtf16Units()
    {
        var name = string.Concat(Enumerable.Repeat("🏪", 200));
        Assert.Equal(name, new Organization(Guid.NewGuid(), name).Name);
        Assert.Throws<ArgumentException>(() => new Organization(Guid.NewGuid(), name + "a"));
        Assert.Throws<ArgumentException>(() => new Organization(Guid.NewGuid(), "\uD800"));
    }

    [Fact]
    public void EmptyIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Organization(Guid.Empty, "Store"));
        Assert.Throws<ArgumentException>(() => new Branch(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "MAIN", "Store", "Etc/UTC"));
        Assert.Throws<ArgumentException>(() => new Branch(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "MAIN", "Store", "Etc/UTC"));
        Assert.Throws<ArgumentException>(() => new Branch(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "MAIN", "Store", "Etc/UTC"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("main")]
    [InlineData("_MAIN")]
    [InlineData("MAIN BRANCH")]
    [InlineData("ŞEKI")]
    public void InvalidBranchCodesAreRejected(string code)
    {
        Assert.Throws<ArgumentException>(() => new Branch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), code, "Store", "Etc/UTC"));
    }

    [Fact]
    public void BranchChangesPreserveTenantAndIdentity()
    {
        var branch = new Branch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MAIN_1-A", "Store", "Asia/Tbilisi", Guid.NewGuid());
        var changed = branch.Rename("New name").Deactivate();
        Assert.Equal(branch.OrganizationId, changed.OrganizationId);
        Assert.Equal(branch.BusinessId, changed.BusinessId);
        Assert.Equal(branch.RegionId, changed.RegionId);
        Assert.Equal("Asia/Tbilisi", changed.TimeZoneId);
        Assert.Equal(branch.Id, changed.Id);
        Assert.Equal(branch.Code, changed.Code);
        Assert.False(changed.IsActive);
        Assert.True(branch.IsActive);
        Assert.Equal("Store", branch.Name);
    }

    [Fact]
    public void SameBranchIdInDifferentOrganizationsIsNotEqual()
    {
        var id = Guid.NewGuid();
        Assert.NotEqual(new Branch(Guid.NewGuid(), Guid.NewGuid(), id, "MAIN", "Store", "Etc/UTC"),
            new Branch(Guid.NewGuid(), Guid.NewGuid(), id, "MAIN", "Store", "Etc/UTC"));
    }
}
