using System.Threading.Tasks;
using NVGInventory.Data;
using Xunit;

namespace NVGInventory.Tests;

public abstract class SqlServerIntegrationTestBase : IAsyncLifetime
{
    protected SqlServerIntegrationFixture Fixture { get; }

    protected SqlServerIntegrationTestBase(SqlServerIntegrationFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (Fixture.IsEnabled)
        {
            await Fixture.ResetDatabaseAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected InventoryDbContext CreateDbContext()
    {
        return Fixture.CreateDbContext();
    }
}
