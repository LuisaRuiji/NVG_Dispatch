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

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class RequestLoanQueryTests : IAsyncLifetime
{
    private const string DefaultPassword = "P@ssw0rd!";
    private const string TestSigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";

    private readonly SqlServerIntegrationFixture _fixture;

    public RequestLoanQueryTests(SqlServerIntegrationFixture fixture)
    {
        _fixture = fixture;
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
    public async Task GetRequests_FiltersByTypeAndStatus()
    {
        var seed = await SeedRequestsAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, seed.RequesterUsername, DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/requests?type=MAINTENANCE_ISSUE&status=PENDING_IO");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var items = root.GetProperty("items");

        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Single(items.EnumerateArray());

        var item = items.EnumerateArray().First();
        Assert.Equal(seed.MaintenanceRequestId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("MAINTENANCE_ISSUE", item.GetProperty("requestType").GetString());
        Assert.Equal("PENDING_IO", item.GetProperty("status").GetString());
    }

    [SqlServerFact]
    public async Task GetRequest_ReturnsLinesAndApprovalState()
    {
        var seed = await SeedRequestsAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, seed.RequesterUsername, DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/requests/{seed.MaintenanceRequestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(seed.MaintenanceRequestId.ToString(), root.GetProperty("id").GetString());
        var approval = root.GetProperty("approval");
        Assert.Equal("PENDING", approval.GetProperty("status").GetString());
        Assert.Equal(RoleNames.InventoryOfficer, approval.GetProperty("nextApproverRole").GetString());

        var lines = root.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(seed.ConsumableItemId.ToString(), lines[0].GetProperty("inventoryId").GetString());
    }

    [SqlServerFact]
    public async Task GetLoans_ReturnsLinesAndReturns()
    {
        var seed = await SeedLoanAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, seed.InventoryOfficerUsername, DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/loans?status=PARTIALLY_RETURNED");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using (var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()))
        {
            var items = listDoc.RootElement.GetProperty("items");
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            Assert.Single(items.EnumerateArray());
            Assert.Equal(seed.LoanId.ToString(), items.EnumerateArray().First().GetProperty("id").GetString());
        }

        var detailResponse = await client.GetAsync($"/api/loans/{seed.LoanId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var doc = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var lines = root.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(seed.LoanLineId.ToString(), lines[0].GetProperty("id").GetString());

        var returns = lines[0].GetProperty("returns");
        Assert.Equal(1, returns.GetArrayLength());
        Assert.Equal("GOOD", returns[0].GetProperty("condition").GetString());
        Assert.Equal(seed.InventoryOfficerId.ToString(), returns[0].GetProperty("receivedByUserId").GetString());
    }

    [SqlServerFact]
    public async Task GetLoans_AllowsManager()
    {
        var seed = await SeedLoanAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, seed.ManagerUsername, DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/loans");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using (var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync()))
        {
            var items = listDoc.RootElement.GetProperty("items");
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            Assert.Contains(items.EnumerateArray(), item => item.GetProperty("id").GetString() == seed.LoanId.ToString());
        }

        var detailResponse = await client.GetAsync($"/api/loans/{seed.LoanId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [SqlServerFact]
    public async Task GetLoans_RejectsDriver()
    {
        var seed = await SeedLoanAsync();

        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client, seed.DriverUsername, DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/loans");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var detailResponse = await client.GetAsync($"/api/loans/{seed.LoanId}");
        Assert.Equal(HttpStatusCode.Forbidden, detailResponse.StatusCode);
    }

    private async Task<RequestSeedResult> SeedRequestsAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var requester = new User
        {
            Id = Guid.NewGuid(),
            Username = "requester_query",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(requester);

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-QUERY-1",
            AssetType = AssetType.Truck,
            Status = AssetStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var consumable = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Engine Oil",
            Unit = "L",
            ItemType = ItemType.Consumable,
            Quantity = 50m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var nonConsumable = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Tool Kit",
            Unit = "SET",
            ItemType = ItemType.NonConsumable,
            Quantity = 5m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Assets.Add(asset);
        context.InventoryItems.AddRange(consumable, nonConsumable);
        await context.SaveChangesAsync();

        var requestService = new RequestService(context);
        var approvalService = new ApprovalService(context, new UserService(context));
        var workflowService = new RequestWorkflowService(
            context,
            requestService,
            approvalService,
            new StockLedgerService(context),
            new UserService(context));

        var maintenance = await workflowService.SubmitMaintenanceIssueAsync(
            new SubmitMaintenanceIssueCommand(
                requester.Id,
                asset.Id,
                "Maintenance",
                new[] { new RequestLineInput(consumable.Id, 1m, null) }));

        var borrowRequest = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.Borrow,
                requester.Id,
                null,
                "Borrow",
                new[] { new RequestLineInput(nonConsumable.Id, 1m, null) }));

        await workflowService.SubmitRequest(borrowRequest.Id);

        return new RequestSeedResult(
            requester.Username,
            maintenance.RequestId,
            consumable.Id);
    }

    private async Task<LoanSeedResult> SeedLoanAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var requester = new User
        {
            Id = Guid.NewGuid(),
            Username = "loan_requester",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var inventoryOfficer = new User
        {
            Id = Guid.NewGuid(),
            Username = "loan_io",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "loan_manager",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = "loan_driver",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.AddRange(requester, inventoryOfficer, manager, driver);

        var ioRoleId = await context.Roles.Where(r => r.Name == RoleNames.InventoryOfficer).Select(r => r.Id).SingleAsync();
        var managerRoleId = await context.Roles.Where(r => r.Name == RoleNames.Manager).Select(r => r.Id).SingleAsync();
        var driverRoleId = await context.Roles.Where(r => r.Name == RoleNames.Driver).Select(r => r.Id).SingleAsync();

        context.UserRoles.AddRange(
            new UserRole { UserId = inventoryOfficer.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId },
            new UserRole { UserId = driver.Id, RoleId = driverRoleId });

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Portable Winch",
            Unit = "PCS",
            ItemType = ItemType.NonConsumable,
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

        var request = await requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.Borrow,
                requester.Id,
                null,
                "Borrow",
                new[] { new RequestLineInput(item.Id, 2m, null) }));

        await workflowService.SubmitRequest(request.Id);

        var requestLineId = request.Lines.Single().Id;
        await workflowService.InventoryOfficerReviewAsync(
            request.Id,
            inventoryOfficer.Id,
            new[] { (requestLineId, 2m, (string?)null) },
            null);

        await workflowService.ManagerDecisionAsync(
            request.Id,
            manager.Id,
            ApprovalDecision.Approve,
            null);

        await workflowService.IssueRequest(request.Id, inventoryOfficer.Id);

        var loanId = await context.Loans.Where(l => l.RequestId == request.Id).Select(l => l.Id).SingleAsync();
        var loanLineId = await context.LoanLines.Where(l => l.LoanId == loanId).Select(l => l.Id).SingleAsync();

        var loanWorkflowService = new LoanWorkflowService(
            context,
            new StockLedgerService(context),
            new UserService(context));

        await loanWorkflowService.ReturnLoanAsync(
            loanId,
            inventoryOfficer.Id,
            new[] { new ReturnLoanLineInput(loanLineId, 1m, ReturnCondition.Good, null) });

        return new LoanSeedResult(
            inventoryOfficer.Id,
            inventoryOfficer.Username,
            manager.Username,
            driver.Username,
            loanId,
            loanLineId);
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

    private sealed record RequestSeedResult(
        string RequesterUsername,
        Guid MaintenanceRequestId,
        Guid ConsumableItemId);

    private sealed record LoanSeedResult(
        Guid InventoryOfficerId,
        string InventoryOfficerUsername,
        string ManagerUsername,
        string DriverUsername,
        Guid LoanId,
        Guid LoanLineId);
}
