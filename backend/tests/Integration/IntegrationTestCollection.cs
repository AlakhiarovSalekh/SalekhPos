using Xunit;

namespace SalekhPos.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<AccessFixture>
{
    public const string Name = "PostgreSQL integration";
}
