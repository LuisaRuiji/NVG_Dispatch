using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using Xunit;
using Xunit.Abstractions;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class ReportsIntegrationTests : IAsyncLifetime
{
    private const string DefaultPassword = "P@ssw0rd!";
    private const string TestSigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";

    private readonly SqlServerIntegrationFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ReportsIntegrationTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        if (_fixture.IsEnabled)
        {
            await _fixture.ResetDatabaseAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [SqlServerFact]
    public async Task InventoryValuationReport_OutputsSampleJson()
    {
        await SeedInventoryAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/inventory-valuation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Inventory valuation JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() >= 2);
    }

    [SqlServerFact]
    public async Task InventoryValuationReport_OutputsCsv()
    {
        await SeedInventoryAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/inventory-valuation.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Inventory valuation CSV:");
        _output.WriteLine(csv);

        Assert.Contains("InventoryId,InventoryName,Quantity,AverageCost,TotalValue", csv);
        Assert.True(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
    }

    [SqlServerFact]
    public async Task AssetMaintenanceCost_UsesSnapshot()
    {
        var assetId = await SeedMaintenanceIssueWithSnapshotAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/reports/asset-maintenance-cost?assetId={assetId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Asset maintenance cost JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var items = root.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        var entry = items.EnumerateArray().Single();
        var totalCost = entry.GetProperty("totalMaintenanceCost").GetDecimal();
        Assert.Equal(500m, totalCost);
    }

    [SqlServerFact]
    public async Task AssetMaintenanceCost_OutputsCsv()
    {
        var assetId = await SeedMaintenanceIssueWithSnapshotAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/reports/asset-maintenance-cost.csv?assetId={assetId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Asset maintenance cost CSV:");
        _output.WriteLine(csv);

        Assert.Contains("AssetId,AssetCode,TotalMaintenanceCost,TotalItemsConsumed,RequestCount", csv);
        Assert.True(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
    }

    [SqlServerFact]
    public async Task AssetMaintenanceCost_UsesSnapshotsAcrossMultipleIssues()
    {
        var assetId = await SeedMaintenanceIssueWithTwoSnapshotsAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/reports/asset-maintenance-cost?assetId={assetId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Asset maintenance cost JSON (multi):");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var items = root.GetProperty("items");
        var entry = items.EnumerateArray().Single();
        var totalCost = entry.GetProperty("totalMaintenanceCost").GetDecimal();
        Assert.Equal(900m, totalCost);
    }

    [SqlServerFact]
    public async Task AssetConsumption_OutputsSampleJson()
    {
        var assetId = await SeedMaintenanceIssueWithSnapshotAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/reports/asset-consumption?assetId={assetId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Asset consumption JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        var entry = root.EnumerateArray().First();
        Assert.True(entry.TryGetProperty("inventoryId", out _));
        Assert.True(entry.TryGetProperty("inventoryName", out _));
    }

    [SqlServerFact]
    public async Task AdjustmentReport_OutputsSampleJson()
    {
        await SeedAdjustmentIssueAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/adjustments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Adjustment report JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        var entry = root.EnumerateArray().First();
        Assert.True(entry.TryGetProperty("requestId", out _));
        Assert.True(entry.TryGetProperty("quantity", out _));
    }

    [SqlServerFact]
    public async Task SupplierSpendReport_OutputsSampleJson()
    {
        await SeedSupplierSpendAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/supplier-spend?basis=ORDERED&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Supplier spend JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        var items = root.GetProperty("items");
        var entry = items.EnumerateArray().First();
        Assert.True(entry.TryGetProperty("basis", out _));
        Assert.True(entry.TryGetProperty("supplierId", out _));
        Assert.True(entry.TryGetProperty("totalSpend", out _));
        Assert.True(entry.TryGetProperty("totalPurchaseOrders", out _));
        Assert.True(entry.TryGetProperty("totalLines", out _));
        Assert.True(entry.TryGetProperty("totalQty", out _));
        Assert.True(entry.TryGetProperty("averagePurchaseOrderValue", out _));
    }

    [SqlServerFact]
    public async Task SupplierSpendReport_OutputsCsv()
    {
        await SeedSupplierSpendAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/supplier-spend.csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Supplier spend CSV:");
        _output.WriteLine(csv);

        Assert.Contains("Basis,SupplierId,SupplierName,TotalSpend,TotalPurchaseOrders,TotalLines,TotalQty,AveragePurchaseOrderValue", csv);
        Assert.True(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
    }

    [SqlServerFact]
    public async Task SupplierSpendReport_OrderedBasis_UsesQtyOrdered()
    {
        await SeedSupplierSpendPartialReceiptAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/supplier-spend?basis=ORDERED&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<SupplierSpendReportItemResponse>>();
        Assert.NotNull(payload);

        var entry = payload!.Items.First();
        Assert.Equal("ORDERED", entry.Basis);
        Assert.Equal(100m, entry.TotalSpend);
        Assert.Equal(10m, entry.TotalQty);
    }

    [SqlServerFact]
    public async Task SupplierSpendReport_ReceivedBasis_UsesReceipts()
    {
        await SeedSupplierSpendPartialReceiptAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/supplier-spend?basis=RECEIVED&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<SupplierSpendReportItemResponse>>();
        Assert.NotNull(payload);

        var entry = payload!.Items.First();
        Assert.Equal("RECEIVED", entry.Basis);
        Assert.Equal(40m, entry.TotalSpend);
        Assert.Equal(4m, entry.TotalQty);
    }

    [SqlServerFact]
    public async Task IntegrityCheckReport_OutputsSampleJson()
    {
        await SeedInventoryAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();

        var token = await LoginAsync(client, "report_mgr", DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reports/integrity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine("Integrity check JSON:");
        _output.WriteLine(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("maintenanceWithoutAsset", out _));
        Assert.True(root.TryGetProperty("loansOverReturned", out _));
        Assert.True(root.TryGetProperty("poOverReceived", out _));
        Assert.True(root.TryGetProperty("negativeInventoryCount", out _));
        Assert.True(root.TryGetProperty("requestsIssuedWithoutStockLogs", out _));
        Assert.True(root.TryGetProperty("duplicateIssueLogs", out _));
        Assert.True(root.TryGetProperty("orphanStockLogs", out _));
        Assert.True(root.TryGetProperty("orphanApprovalActions", out _));
        Assert.True(root.TryGetProperty("poWithoutWorkflow", out _));
        Assert.True(root.TryGetProperty("adjustmentWithoutLogs", out _));
        Assert.True(root.TryGetProperty("supplierActionsWithoutAudit", out _));
        Assert.True(root.TryGetProperty("poReceiptsWithoutAudit", out _));
        Assert.True(root.TryGetProperty("loanReturnsWithoutAudit", out _));
        Assert.True(root.TryGetProperty("adjustmentsWithoutAudit", out _));
        Assert.True(root.TryGetProperty("inactiveSuppliersReferenced", out _));
        Assert.True(root.TryGetProperty("inactiveInventoryReferenced", out _));
    }

    private async Task SeedInventoryAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(manager);

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.Add(new UserRole
        {
            UserId = manager.Id,
            RoleId = managerRoleId
        });

        context.InventoryItems.AddRange(
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Valuation Item A",
                Unit = "PCS",
                ItemType = Domain.Enums.ItemType.Consumable,
                Quantity = 10m,
                AverageCost = 12.3456m,
                LastCost = 12.3456m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = "Valuation Item B",
                Unit = "PCS",
                ItemType = Domain.Enums.ItemType.Consumable,
                Quantity = 3m,
                AverageCost = 99.9m,
                LastCost = 99.9m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedMaintenanceIssueWithSnapshotAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var requester = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_req",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_io",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(requester, ioUser, manager);

        var ioRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.InventoryOfficer)
            .Select(role => role.Id)
            .FirstAsync();

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-VAL",
            AssetType = Domain.Enums.AssetType.Truck
        };

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Service Oil",
            Unit = "L",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 5m,
            AverageCost = 100m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Assets.Add(asset);
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var requestService = new RequestService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var request = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.MaintenanceIssue,
                requester.Id,
                asset.Id,
                "Cost Snapshot",
                new[] { new RequestLineInput(item.Id, 5m, null) }));

        await workflowService.SubmitRequest(request.Id);
        var lineId = request.Lines.Single().Id;
        await workflowService.InventoryOfficerReviewAsync(
            request.Id,
            ioUser.Id,
            new[] { (lineId, 5m, (string?)null) },
            null);

        await workflowService.ManagerDecisionAsync(
            request.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);

        await workflowService.IssueRequest(request.Id, ioUser.Id);

        var ledger = new StockLedgerService(context);
        await ledger.ApplyMovement(
            StockMovementType.In,
            item.Id,
            5m,
            EntityTypes.PurchaseOrder,
            Guid.NewGuid(),
            ioUser.Id,
            200m);

        return asset.Id;
    }

    private async Task<Guid> SeedMaintenanceIssueWithTwoSnapshotsAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var requester = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_req2",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_io2",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(requester, ioUser, manager);

        var ioRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.InventoryOfficer)
            .Select(role => role.Id)
            .FirstAsync();

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-VAL-2",
            AssetType = Domain.Enums.AssetType.Truck
        };

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Service Oil",
            Unit = "L",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 5m,
            AverageCost = 100m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Assets.Add(asset);
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var requestService = new RequestService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var request1 = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.MaintenanceIssue,
                requester.Id,
                asset.Id,
                "Cost Snapshot 1",
                new[] { new RequestLineInput(item.Id, 5m, null) }));

        await workflowService.SubmitRequest(request1.Id);
        var line1 = request1.Lines.Single().Id;
        await workflowService.InventoryOfficerReviewAsync(
            request1.Id,
            ioUser.Id,
            new[] { (line1, 5m, (string?)null) },
            null);

        await workflowService.ManagerDecisionAsync(
            request1.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);

        await workflowService.IssueRequest(request1.Id, ioUser.Id);

        var ledger = new StockLedgerService(context);
        await ledger.ApplyMovement(
            StockMovementType.In,
            item.Id,
            5m,
            EntityTypes.PurchaseOrder,
            Guid.NewGuid(),
            ioUser.Id,
            200m);

        var request2 = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.MaintenanceIssue,
                requester.Id,
                asset.Id,
                "Cost Snapshot 2",
                new[] { new RequestLineInput(item.Id, 2m, null) }));

        await workflowService.SubmitRequest(request2.Id);
        var line2 = request2.Lines.Single().Id;
        await workflowService.InventoryOfficerReviewAsync(
            request2.Id,
            ioUser.Id,
            new[] { (line2, 2m, (string?)null) },
            null);

        await workflowService.ManagerDecisionAsync(
            request2.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);

        await workflowService.IssueRequest(request2.Id, ioUser.Id);

        return asset.Id;
    }

    private async Task SeedAdjustmentIssueAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_io_adj",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(ioUser, manager);

        var ioRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.InventoryOfficer)
            .Select(role => role.Id)
            .FirstAsync();

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId });

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Adjustment Item",
            Unit = "PCS",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 5m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var requestService = new RequestService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var submit = await workflowService.SubmitAdjustmentAsync(
            new SubmitAdjustmentCommand(
                ioUser.Id,
                "Damaged stock",
                new[] { new RequestLineInput(item.Id, -2m, null) }));

        var request = await context.Requests.Include(r => r.Lines).SingleAsync(r => r.Id == submit.RequestId);
        await workflowService.InventoryOfficerReviewAsync(
            request.Id,
            ioUser.Id,
            new[] { (request.Lines.Single().Id, -2m, (string?)null) },
            "Reviewed");

        await workflowService.ManagerDecisionAsync(request.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.IssueRequest(request.Id, ioUser.Id);
    }

    private async Task SeedSupplierSpendAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_io_spend",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var finance = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_fin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var ceo = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_ceo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(ioUser, manager, finance, ceo);

        var ioRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.InventoryOfficer)
            .Select(role => role.Id)
            .FirstAsync();

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        var financeRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.HeadOfFinance)
            .Select(role => role.Id)
            .FirstAsync();

        var ceoRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Ceo)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId },
            new UserRole { UserId = finance.Id, RoleId = financeRoleId },
            new UserRole { UserId = ceo.Id, RoleId = ceoRoleId });

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Spend Supplier",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var itemA = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Spend Item A",
            Unit = "PCS",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var itemB = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Spend Item B",
            Unit = "PCS",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Suppliers.Add(supplier);
        context.InventoryItems.AddRange(itemA, itemB);
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new PurchaseOrderWorkflowService(
            context,
            poService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var draft = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                "Spend PO",
                new[]
                {
                    new PurchaseOrderLineInput(itemA.Id, 5m, 10m, null),
                    new PurchaseOrderLineInput(itemB.Id, 4m, 20m, null)
                }));

        await workflowService.SubmitAsync(draft.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(draft.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(draft.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(draft.Id, ceo.Id, ApprovalDecision.Approve, null);

        var lineA = draft.Lines.Single(line => line.InventoryId == itemA.Id);
        var lineB = draft.Lines.Single(line => line.InventoryId == itemB.Id);

        await workflowService.ReceiveAsync(
            draft.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(lineA.Id, 2m, null) },
            "partial");

        await workflowService.ReceiveAsync(
            draft.Id,
            ioUser.Id,
            new[]
            {
                new PurchaseOrderReceiveLineInput(lineA.Id, 3m, null),
                new PurchaseOrderReceiveLineInput(lineB.Id, 4m, null)
            },
            "final");
    }

    private async Task SeedSupplierSpendPartialReceiptAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var ioUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_io_spend_partial",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_mgr",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var finance = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_fin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var ceo = new User
        {
            Id = Guid.NewGuid(),
            Username = "report_ceo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(ioUser, manager, finance, ceo);

        var ioRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.InventoryOfficer)
            .Select(role => role.Id)
            .FirstAsync();

        var managerRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Manager)
            .Select(role => role.Id)
            .FirstAsync();

        var financeRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.HeadOfFinance)
            .Select(role => role.Id)
            .FirstAsync();

        var ceoRoleId = await context.Roles
            .Where(role => role.Name == RoleNames.Ceo)
            .Select(role => role.Id)
            .FirstAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = ioUser.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId },
            new UserRole { UserId = finance.Id, RoleId = financeRoleId },
            new UserRole { UserId = ceo.Id, RoleId = ceoRoleId });

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Spend Supplier Partial",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var itemA = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Spend Item Partial",
            Unit = "PCS",
            ItemType = Domain.Enums.ItemType.Consumable,
            Quantity = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Suppliers.Add(supplier);
        context.InventoryItems.Add(itemA);
        await context.SaveChangesAsync();

        var poService = new PurchaseOrderService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new PurchaseOrderWorkflowService(
            context,
            poService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var draft = await workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUser.Id,
                supplier.Id,
                "Spend PO Partial",
                new[]
                {
                    new PurchaseOrderLineInput(itemA.Id, 10m, 10m, null)
                }));

        await workflowService.SubmitAsync(draft.Id, ioUser.Id);
        await workflowService.ApplyDecisionAsync(draft.Id, manager.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(draft.Id, finance.Id, ApprovalDecision.Approve, null);
        await workflowService.ApplyDecisionAsync(draft.Id, ceo.Id, ApprovalDecision.Approve, null);

        var lineA = draft.Lines.Single();

        await workflowService.ReceiveAsync(
            draft.Id,
            ioUser.Id,
            new[] { new PurchaseOrderReceiveLineInput(lineA.Id, 4m, null) },
            "partial");
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Login did not return an access token.");
        }

        return payload.AccessToken;
    }
}
