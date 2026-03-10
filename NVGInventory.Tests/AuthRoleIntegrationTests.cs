using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class AuthRoleIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationFixture _fixture;
    private const string DefaultPassword = "P@ssw0rd!";
    private const string TestSigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";

    public AuthRoleIntegrationTests(SqlServerIntegrationFixture fixture)
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
    public async Task Manager_can_approve_and_io_can_issue_maintenance_request()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedAsync();

        using var loginClient = factory.CreateClient();
        var requesterToken = await LoginAsync(loginClient, seed.RequesterUsername, DefaultPassword);
        var ioToken = await LoginAsync(loginClient, seed.InventoryOfficerUsername, DefaultPassword);
        var managerToken = await LoginAsync(loginClient, seed.ManagerUsername, DefaultPassword);

        using var requesterClient = factory.CreateClient();
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);

        var submitResponse = await requesterClient.PostAsJsonAsync("/api/requests/maintenance-issue", new
        {
            assetId = seed.AssetId,
            purpose = "Routine service",
            lines = new[]
            {
                new
                {
                    inventoryId = seed.InventoryId,
                    quantity = 2m,
                    remarks = (string?)null
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        Guid requestId;
        Guid requestLineId;
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var request = await dbContext.Requests
                .Include(r => r.Lines)
                .SingleAsync(r => r.RequesterUserId == seed.RequesterId);
            requestId = request.Id;
            requestLineId = request.Lines.Single().Id;
        }

        using var ioClient = factory.CreateClient();
        ioClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ioToken);
        var ioReviewResponse = await ioClient.PostAsJsonAsync($"/api/requests/{requestId}/io-review", new
        {
            ioRemarks = "approved",
            lines = new[]
            {
                new
                {
                    requestLineId,
                    qtyApproved = 2m,
                    remarks = (string?)null
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, ioReviewResponse.StatusCode);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var managerResponse = await managerClient.PostAsJsonAsync($"/api/requests/{requestId}/manager-decision", new
        {
            decision = "APPROVE",
            remarks = "ok"
        });

        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);

        var issueResponse = await ioClient.PostAsync($"/api/requests/{requestId}/issue", null);
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var request = await dbContext.Requests.SingleAsync(r => r.Id == requestId);
            Assert.Equal(RequestStatus.Closed, request.Status);
        }
    }

    private async Task<SeedResult> SeedAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();

        var inventoryOfficer = new User
        {
            Id = Guid.NewGuid(),
            Username = "io_test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = "manager_test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var requester = new User
        {
            Id = Guid.NewGuid(),
            Username = "requester_test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        dbContext.Users.AddRange(inventoryOfficer, manager, requester);

        var ioRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.InventoryOfficer).Select(r => r.Id).SingleAsync();
        var managerRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.Manager).Select(r => r.Id).SingleAsync();

        dbContext.UserRoles.AddRange(
            new UserRole { UserId = inventoryOfficer.Id, RoleId = ioRoleId },
            new UserRole { UserId = manager.Id, RoleId = managerRoleId });

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "TRK-TEST-1",
            AssetType = AssetType.Truck,
            Status = AssetStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = "Engine Oil",
            Unit = "L",
            ItemType = ItemType.Consumable,
            Quantity = 25m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Assets.Add(asset);
        dbContext.InventoryItems.Add(item);

        await dbContext.SaveChangesAsync();

        return new SeedResult(
            requester.Id,
            requester.Username,
            inventoryOfficer.Username,
            manager.Username,
            asset.Id,
            item.Id);
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

    private sealed record SeedResult(
        Guid RequesterId,
        string RequesterUsername,
        string InventoryOfficerUsername,
        string ManagerUsername,
        Guid AssetId,
        Guid InventoryId);
}
