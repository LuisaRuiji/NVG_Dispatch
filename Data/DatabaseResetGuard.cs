using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace NVGInventory.Data;

public static class DatabaseResetGuard
{
    private static readonly string[] SafeDatabaseNameSuffixes =
    [
        "_Dev",
        "_Demo",
        "_Perf",
        "_Tests"
    ];

    public static void EnsureSafeToReset(IHostEnvironment environment, InventoryDbContext dbContext)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Database reset is allowed only in Development.");
        }

        var databaseName = dbContext.Database.GetDbConnection().Database;
        EnsureSafeDatabaseName(databaseName);
    }

    public static void EnsureSafeDatabaseName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database reset refused: connection string must specify a database name.");
        }

        var trimmed = databaseName.Trim();
        if (!SafeDatabaseNameSuffixes.Any(suffix => trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixList = string.Join(", ", SafeDatabaseNameSuffixes);
            throw new InvalidOperationException(
                $"Database reset refused for '{trimmed}'. Destructive reseeding requires a database name ending in one of: {suffixList}.");
        }
    }
}
