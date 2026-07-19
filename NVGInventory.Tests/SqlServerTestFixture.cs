using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using Xunit;

namespace NVGInventory.Tests;

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    private const string TestJwtIssuer = "NVGInventory.Test";
    private const string TestJwtAudience = "NVGInventory.Test";
    private const string TestJwtKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";
    private const string TestFrontendBaseUrl = "http://localhost:5173";
    private readonly string? _connectionString;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_connectionString);
    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("TEST_SQLSERVER_CONNECTION_STRING is not set.");

    public SqlServerIntegrationFixture()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("FrontendBaseUrl", TestFrontendBaseUrl);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", TestFrontendBaseUrl);

        var raw = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(raw);
        var hasEncryptSetting = raw.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase);
        if (!hasEncryptSetting)
        {
            builder.Encrypt = false;
            builder.TrustServerCertificate = true;
        }
        var dataSource = builder.DataSource?.Trim() ?? string.Empty;
        if (dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to run tests against LocalDB. Use a SQL Server instance and a *_Tests database.");
        }

        var databaseName = builder.InitialCatalog?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("TEST_SQLSERVER_CONNECTION_STRING must specify a database name.");
        }

        if (!databaseName.EndsWith("_Tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to run tests: database name must end with _Tests.");
        }

        _connectionString = builder.ConnectionString;
    }

    public DbContextOptions<InventoryDbContext> Options
    {
        get
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException("TEST_SQLSERVER_CONNECTION_STRING is not set.");
            }

            return new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlServer(_connectionString!)
                .Options;
        }
    }

    public InventoryDbContext CreateDbContext()
    {
        return new InventoryDbContext(Options);
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task ResetDatabaseAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        await using var context = new InventoryDbContext(Options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await EnsureSeededAsync(context);
    }

    private static async Task EnsureSeededAsync(InventoryDbContext context)
    {
        var requiredRoles = new[]
        {
            RoleNames.InventoryOfficer,
            RoleNames.Manager,
            RoleNames.HeadOfFinance,
            RoleNames.Ceo,
            RoleNames.Driver,
            RoleNames.Dispatcher,
            RoleNames.Customer,
            RoleNames.Owner
        };

        var existingRoles = await context.Roles.Select(role => role.Name).ToListAsync();
        foreach (var roleName in requiredRoles.Except(existingRoles))
        {
            context.Roles.Add(new Role
            {
                Id = 0,
                Name = roleName
            });
        }

        var requiredWorkflows = new[]
        {
            WorkflowKeys.MaintenanceIssueApproval,
            WorkflowKeys.BorrowApproval,
            WorkflowKeys.AdjustmentApproval,
            WorkflowKeys.PoApproval
        };

        var existingWorkflows = await context.Workflows.Select(workflow => workflow.WorkflowKey).ToListAsync();
        foreach (var workflowKey in requiredWorkflows.Except(existingWorkflows))
        {
            context.Workflows.Add(new Workflow
            {
                WorkflowKey = workflowKey,
                Name = workflowKey.Replace('_', ' '),
                IsActive = true
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }
}
