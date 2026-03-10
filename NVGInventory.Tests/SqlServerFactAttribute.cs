using System;
using Xunit;

namespace NVGInventory.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "TEST_SQLSERVER_CONNECTION_STRING is not set. Skipping SQL Server integration tests.";
        }
    }
}
