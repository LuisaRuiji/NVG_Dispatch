using Xunit;

namespace NVGInventory.Tests;

[CollectionDefinition("SqlServerIntegration")]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
}
