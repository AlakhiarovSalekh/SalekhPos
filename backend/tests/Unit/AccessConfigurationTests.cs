using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Infrastructure;
using Xunit;

namespace SalekhPos.Tests;

public sealed class AccessConfigurationTests
{
    [Theory]
    [InlineData("Username=postgres;Database=test;Host=localhost")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=remote.example;SSL Mode=Disable")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;Timeout=0")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;Command Timeout=0")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;No Reset On Close=true")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;Pooling=false")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;Multiplexing=true")]
    [InlineData("Username=salekhpos_runtime;Database=test;Host=localhost;Maximum Pool Size=101")]
    public void UnsafeDatabaseConfigurationsFailBeforeConnection(string configuration)
    {
        Assert.Throws<InvalidOperationException>(() => new AccessDatabase(configuration, true));
    }

    [Fact]
    public void ProductionRequiresVerifiedTlsEvenForLoopback()
    {
        Assert.Throws<InvalidOperationException>(() => new AccessDatabase(
            "Username=salekhpos_runtime;Database=test;Host=localhost;SSL Mode=Disable", false));
    }

    [Theory]
    [InlineData("", "subject")]
    [InlineData("issuer", "")]
    [InlineData("issuer\n", "subject")]
    [InlineData("issuer", "subject\n")]
    public void InvalidIdentityCannotEnterDataBoundary(string issuer, string subject)
    {
        Assert.Throws<ArgumentException>(() => new AccessIdentity(issuer, subject));
    }
}
