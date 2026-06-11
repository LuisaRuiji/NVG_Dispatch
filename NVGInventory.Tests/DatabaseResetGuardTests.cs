using System;
using NVGInventory.Data;
using Xunit;

namespace NVGInventory.Tests;

public sealed class DatabaseResetGuardTests
{
    [Theory]
    [InlineData("NVG_Inventory_Dev")]
    [InlineData("NVG_Inventory_Demo")]
    [InlineData("NVG_Inventory_Perf")]
    [InlineData("NVG_Inventory_Tests")]
    public void EnsureSafeDatabaseName_AllowsDisposableDatabaseNames(string databaseName)
    {
        DatabaseResetGuard.EnsureSafeDatabaseName(databaseName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NVG_Inventory")]
    [InlineData("NVG_Inventory_Production")]
    [InlineData("CustomerDispatch")]
    public void EnsureSafeDatabaseName_RejectsAmbiguousOrProductionLikeDatabaseNames(string databaseName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DatabaseResetGuard.EnsureSafeDatabaseName(databaseName));

        Assert.Contains("Database reset refused", ex.Message);
    }
}
