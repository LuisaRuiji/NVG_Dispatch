using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using Xunit;

namespace NVGInventory.Tests;

public sealed class ReportQueryServiceAuditTests
{
    private static readonly Guid AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ManagerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid FinanceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid InventoryOfficerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid DriverId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid AssignedTripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTripId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(RoleNames.SuperAdmin)]
    [InlineData(RoleNames.Admin)]
    public async Task GetAuditLogsAsync_AdminRolesCanSeeAllAuditEntries(string role)
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);

        var result = await service.GetAuditLogsAsync(
            null,
            null,
            null,
            null,
            null,
            Principal(AdminId, role),
            1,
            20);

        Assert.Equal(8, result.TotalCount);
    }

    [Fact]
    public async Task GetAuditLogsAsync_UsesActorCurrentRolesWhenAuditSnapshotIsBlank()
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);

        var result = await service.GetAuditLogsAsync(
            action: "MANAGER_OWN_ACTION",
            entityType: null,
            entityId: null,
            fromUtc: null,
            toUtc: null,
            user: Principal(AdminId, RoleNames.Admin),
            page: 1,
            pageSize: 20);

        var entry = Assert.Single(result.Items);
        Assert.Equal(RoleNames.Manager, entry.ActorRole);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ManagerSeesDispatchDomainAndOwnActions()
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);
        var principal = Principal(ManagerId, RoleNames.Manager);

        var result = await service.GetAuditLogsAsync(
            null,
            null,
            null,
            null,
            null,
            principal,
            1,
            20);

        var actions = result.Items.Select(item => item.Action).ToHashSet();
        Assert.Contains("DISPATCH_DOMAIN_ACTION", actions);
        Assert.Contains("MANAGER_OWN_ACTION", actions);
        Assert.DoesNotContain("PAYMENT_RECORDED", actions);
        Assert.DoesNotContain("INVENTORY_DOMAIN_ACTION", actions);
    }

    [Fact]
    public async Task GetAuditLogsAsync_HeadOfFinanceSeesFinanceActionsAndOwnActions()
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);

        var result = await service.GetAuditLogsAsync(
            null,
            null,
            null,
            null,
            null,
            Principal(FinanceId, RoleNames.HeadOfFinance),
            1,
            20);

        var actions = result.Items.Select(item => item.Action).ToHashSet();
        Assert.Contains("PAYMENT_RECORDED", actions);
        Assert.Contains("FINANCE_OWN_ACTION", actions);
        Assert.DoesNotContain("DISPATCH_DOMAIN_ACTION", actions);
        Assert.DoesNotContain("INVENTORY_DOMAIN_ACTION", actions);
    }

    [Fact]
    public async Task GetAuditLogsAsync_InventoryOfficerSeesInventoryDomainAndOwnActions()
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);

        var result = await service.GetAuditLogsAsync(
            null,
            null,
            null,
            null,
            null,
            Principal(InventoryOfficerId, RoleNames.InventoryOfficer),
            1,
            20);

        var actions = result.Items.Select(item => item.Action).ToHashSet();
        Assert.Contains("INVENTORY_DOMAIN_ACTION", actions);
        Assert.Contains("INVENTORY_OWN_ACTION", actions);
        Assert.DoesNotContain("DISPATCH_DOMAIN_ACTION", actions);
        Assert.DoesNotContain("PAYMENT_RECORDED", actions);
    }

    [Fact]
    public async Task GetAuditLogsAsync_DriverSeesOnlyOwnActionsForAssignedTrips()
    {
        using var dbContext = CreateDbContext();
        await SeedAuditLogsAsync(dbContext);
        var service = new ReportQueryService(dbContext);

        var result = await service.GetAuditLogsAsync(
            null,
            null,
            null,
            null,
            null,
            Principal(DriverId, RoleNames.Driver),
            1,
            20);

        var entry = Assert.Single(result.Items);
        Assert.Equal("DRIVER_ASSIGNED_TRIP_ACTION", entry.Action);
        Assert.Equal(AssignedTripId, entry.EntityId);
        Assert.Equal(DriverId, entry.ActorUserId);
    }

    private static async Task SeedAuditLogsAsync(InventoryDbContext dbContext)
    {
        dbContext.Roles.AddRange(
            Role(RoleNames.Admin),
            Role(RoleNames.Manager),
            Role(RoleNames.HeadOfFinance),
            Role(RoleNames.InventoryOfficer),
            Role(RoleNames.Driver));
        dbContext.Users.AddRange(
            User(AdminId, "admin"),
            User(ManagerId, "manager"),
            User(FinanceId, "finance"),
            User(InventoryOfficerId, "inventory"),
            User(DriverId, "driver"));
        dbContext.UserRoles.AddRange(
            new UserRole { UserId = AdminId, RoleId = RoleId(RoleNames.Admin) },
            new UserRole { UserId = ManagerId, RoleId = RoleId(RoleNames.Manager) },
            new UserRole { UserId = FinanceId, RoleId = RoleId(RoleNames.HeadOfFinance) },
            new UserRole { UserId = InventoryOfficerId, RoleId = RoleId(RoleNames.InventoryOfficer) },
            new UserRole { UserId = DriverId, RoleId = RoleId(RoleNames.Driver) });

        dbContext.DispatchCustomers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Customer",
            CreatedAt = DateTime.UtcNow
        });
        var customerId = dbContext.DispatchCustomers.Local.Single().Id;

        dbContext.DispatchTrips.AddRange(
            new Trip
            {
                Id = AssignedTripId,
                CustomerId = customerId,
                DriverUserId = DriverId,
                Status = TripStatus.Dispatched,
                CreatedAt = DateTime.UtcNow
            },
            new Trip
            {
                Id = OtherTripId,
                CustomerId = customerId,
                DriverUserId = Guid.NewGuid(),
                Status = TripStatus.Dispatched,
                CreatedAt = DateTime.UtcNow
            });

        dbContext.AuditLogs.AddRange(
            Audit("DISPATCH_DOMAIN_ACTION", EntityTypes.DispatchTrip, Guid.NewGuid(), AdminId),
            Audit("MANAGER_OWN_ACTION", EntityTypes.User, Guid.NewGuid(), ManagerId),
            Audit("PAYMENT_RECORDED", "payment", Guid.NewGuid(), AdminId),
            Audit("FINANCE_OWN_ACTION", EntityTypes.User, Guid.NewGuid(), FinanceId),
            Audit("INVENTORY_DOMAIN_ACTION", "inventory_item", Guid.NewGuid(), AdminId),
            Audit("INVENTORY_OWN_ACTION", EntityTypes.User, Guid.NewGuid(), InventoryOfficerId),
            Audit("DRIVER_ASSIGNED_TRIP_ACTION", EntityTypes.DispatchTrip, AssignedTripId, DriverId),
            Audit("DRIVER_OTHER_TRIP_ACTION", EntityTypes.DispatchTrip, OtherTripId, DriverId));

        await dbContext.SaveChangesAsync();
    }

    private static User User(Guid id, string username)
    {
        return new User
        {
            Id = id,
            Username = username,
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Role Role(string roleName)
    {
        return new Role
        {
            Id = RoleId(roleName),
            Name = roleName
        };
    }

    private static AuditLog Audit(string action, string entityType, Guid entityId, Guid actorUserId)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
            AfterJson = "{}"
        };
    }

    private static int RoleId(string roleName)
    {
        return roleName switch
        {
            RoleNames.InventoryOfficer => 1,
            RoleNames.Manager => 2,
            RoleNames.HeadOfFinance => 3,
            RoleNames.Ceo => 4,
            RoleNames.Driver => 5,
            RoleNames.Admin => 6,
            RoleNames.SuperAdmin => 7,
            RoleNames.Dispatcher => 8,
            RoleNames.Customer => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Unknown role.")
        };
    }

    private static ClaimsPrincipal Principal(Guid userId, string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("role", role),
                new Claim(ClaimTypes.Role, role)
            },
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"report-query-audit-{Guid.NewGuid():N}")
            .Options;

        return new InventoryDbContext(options);
    }
}
