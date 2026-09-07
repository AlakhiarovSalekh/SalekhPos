using SalekhPos.Organizations;
using Xunit;

namespace SalekhPos.Tests;

public sealed class HierarchyTests
{
    [Fact]
    public void BusinessAndRegionChangesPreserveTheirParentsAndIdentity()
    {
        var business = new Business(Guid.NewGuid(), Guid.NewGuid(), "RETAIL", "Retail");
        var changedBusiness = business.Rename("Market").Deactivate();
        Assert.Equal(business.OrganizationId, changedBusiness.OrganizationId);
        Assert.Equal(business.Id, changedBusiness.Id);
        Assert.Equal(business.Code, changedBusiness.Code);
        Assert.False(changedBusiness.IsActive);
        Assert.Equal("Retail", business.Name);
        Assert.True(business.IsActive);

        var region = new Region(business.OrganizationId, business.Id, Guid.NewGuid(), "WEST", "West");
        var changedRegion = region.Rename("Western region").Deactivate();
        Assert.Equal(region.OrganizationId, changedRegion.OrganizationId);
        Assert.Equal(region.BusinessId, changedRegion.BusinessId);
        Assert.Equal(region.Id, changedRegion.Id);
        Assert.Equal(region.Code, changedRegion.Code);
        Assert.False(changedRegion.IsActive);
        Assert.Equal("West", region.Name);
        Assert.True(region.IsActive);
    }

    [Fact]
    public void EmptyBusinessAndRegionIdentifiersAreRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Business(Guid.Empty, id, "MAIN", "Store"));
        Assert.Throws<ArgumentException>(() => new Business(id, Guid.Empty, "MAIN", "Store"));
        Assert.Throws<ArgumentException>(() => new Region(Guid.Empty, id, id, "WEST", "Region"));
        Assert.Throws<ArgumentException>(() => new Region(id, Guid.Empty, id, "WEST", "Region"));
        Assert.Throws<ArgumentException>(() => new Region(id, id, Guid.Empty, "WEST", "Region"));
        Assert.Throws<ArgumentException>(() => NewBranch("Etc/UTC", regionId: Guid.Empty));
    }

    [Fact]
    public void RegionIsOptionalButTimeZoneAndBusinessAreExplicit()
    {
        var branch = NewBranch("Etc/UTC");
        Assert.Null(branch.RegionId);
        Assert.NotEqual(Guid.Empty, branch.BusinessId);
        Assert.Equal("Etc/UTC", branch.TimeZoneId);
        Assert.Throws<ArgumentNullException>(() => NewBranch(null!));
    }

    [Theory]
    [InlineData("Asia/Tbilisi")]
    [InlineData("America/New_York")]
    [InlineData("Pacific/Auckland")]
    [InlineData("Etc/UTC")]
    public void KnownIanaTimeZonesArePreserved(string timeZoneId)
    {
        Assert.Equal(timeZoneId, NewBranch(timeZoneId).TimeZoneId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Asia/Tbilisi ")]
    [InlineData("asia/tbilisi")]
    [InlineData("Eastern Standard Time")]
    [InlineData("UTC+4")]
    [InlineData("+04:00")]
    [InlineData("Mars/Phobos")]
    public void InvalidOrWindowsTimeZonesNeverFallbackToServerLocalTime(string timeZoneId)
    {
        Assert.Throws<ArgumentException>(() => NewBranch(timeZoneId));
    }

    [Fact]
    public void NamedTimeZoneIncludesDaylightSavingRulesInsteadOfFixedOffset()
    {
        var branch = NewBranch("America/New_York");
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);
        Assert.Equal(TimeSpan.FromHours(-5), timeZone.GetUtcOffset(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromHours(-4), timeZone.GetUtcOffset(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    public static TheoryData<string> InvalidNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var character in new[] { '\u0000', '\u0009', '\u000A', '\u001F', '\u007F', '\u0085', '\u009F' })
            {
                names.Add("Store" + character + "Name");
            }
            foreach (var character in new[] { '\u0020', '\u00A0', '\u1680', '\u2000', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000' })
            {
                names.Add(character + "Store");
                names.Add("Store" + character);
                names.Add(character.ToString());
            }
            names.Add("\uD800");
            names.Add(new string('a', 201));
            return names;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void EveryHierarchyLevelUsesTheSameUnicodeNameInvariant(string name)
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Organization(id, name));
        Assert.Throws<ArgumentException>(() => new Business(id, id, "MAIN", name));
        Assert.Throws<ArgumentException>(() => new Region(id, id, id, "MAIN", name));
        Assert.Throws<ArgumentException>(() => new Branch(id, id, id, "MAIN", name, "Etc/UTC"));
    }

    [Fact]
    public void MultilingualAndSupplementaryUnicodeNamesRoundTripWithoutNormalization()
    {
        var id = Guid.NewGuid();
        foreach (var name in new[] { "თბილისის მაღაზია", "Şəki mağazası", "متجر", "Store\u00A0Name", string.Concat(Enumerable.Repeat("🏪", 200)) })
        {
            Assert.Equal(name, new Business(id, id, "MAIN", name).Name);
            Assert.Equal(name, new Region(id, id, id, "MAIN", name).Name);
            Assert.Equal(name, new Branch(id, id, id, "MAIN", name, "Etc/UTC").Name);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("lowercase")]
    [InlineData("_PREFIX")]
    [InlineData("WITH SPACE")]
    [InlineData("MAIN\n")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567")]
    public void CodesShareStrictAsciiValidationAcrossHierarchy(string code)
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Business(id, id, code, "Store"));
        Assert.Throws<ArgumentException>(() => new Region(id, id, id, code, "Region"));
        Assert.Throws<ArgumentException>(() => new Branch(id, id, id, code, "Branch", "Etc/UTC"));
    }

    private static Branch NewBranch(string timeZoneId, Guid? regionId = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MAIN", "Store", timeZoneId, regionId);
}
