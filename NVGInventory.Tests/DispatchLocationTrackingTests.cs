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
using NVGInventory.Modules.Dispatching.Controllers;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public sealed class DispatchLocationTrackingTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationFixture _fixture;
    private const string DefaultPassword = "P@ssw0rd!";
    private const string TestSigningKey = "TESTING_ONLY_SIGNING_KEY_32_CHARS_MIN_123456";

    public DispatchLocationTrackingTests(SqlServerIntegrationFixture fixture)
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
    public async Task Driver_can_start_send_update_and_stop_own_trip_tracking()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        using var loginClient = factory.CreateClient();
        var driverToken = await LoginAsync(loginClient, seed.Driver1Username, DefaultPassword);

        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var startRequest = new StartTrackingRequest(14.5995m, 120.9842m);
        var startResponse = await driverClient.PostAsJsonAsync($"/api/driver/trips/{seed.Trip1Id}/start-tracking", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        Guid sessionId;
        await using (var db = _fixture.CreateDbContext())
        {
            var session = await db.LocationTrackingSessions
                .SingleAsync(s => s.TripId == seed.Trip1Id && s.Status == TrackingSessionStatus.Active);
            
            sessionId = session.Id;
            Assert.Equal(seed.Driver1Id, session.DispatchDriverId);
            Assert.Equal(seed.TruckId, session.DispatchTruckId);
            Assert.Equal(14.5995m, session.StartLatitude);
            Assert.Equal(120.9842m, session.StartLongitude);

            var truck = await db.DispatchTrucks.SingleAsync(t => t.Id == seed.TruckId);
            Assert.Equal(14.5995m, truck.LastLatitude);
            Assert.Equal(120.9842m, truck.LastLongitude);

            var trip = await db.DispatchTrips.SingleAsync(t => t.Id == seed.Trip1Id);
            Assert.Equal(14.5995m, trip.LastLatitude);
            Assert.Equal(120.9842m, trip.LastLongitude);
        }

        var updateRequest = new SendLocationRequest(
            TrackingSessionId: sessionId,
            TripId: seed.Trip1Id,
            DispatchDriverId: seed.Driver1Id,
            DispatchTruckId: seed.TruckId,
            Latitude: 14.6100m,
            Longitude: 120.9900m,
            AccuracyMeters: 5.2m,
            SpeedKph: 55.4m,
            Heading: 180.0m,
            RecordedAt: DateTime.UtcNow,
            Source: LocationUpdateSource.DriverApp
        );

        var updateResponse = await driverClient.PostAsJsonAsync("/api/driver/location", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        await using (var db = _fixture.CreateDbContext())
        {
            var update = await db.DriverLocationUpdates
                .SingleAsync(u => u.TrackingSessionId == sessionId);
            Assert.Equal(14.6100m, update.Latitude);
            Assert.Equal(120.9900m, update.Longitude);
            Assert.Equal(5.2m, update.AccuracyMeters);
            Assert.Equal(55.4m, update.SpeedKph);

            var truck = await db.DispatchTrucks.SingleAsync(t => t.Id == seed.TruckId);
            Assert.Equal(14.6100m, truck.LastLatitude);
            Assert.Equal(120.9900m, truck.LastLongitude);

            var trip = await db.DispatchTrips.SingleAsync(t => t.Id == seed.Trip1Id);
            Assert.Equal(14.6100m, trip.LastLatitude);
            Assert.Equal(120.9900m, trip.LastLongitude);
        }

        var stopRequest = new StopTrackingRequest(14.6200m, 120.9950m);
        var stopResponse = await driverClient.PostAsJsonAsync($"/api/driver/trips/{seed.Trip1Id}/stop-tracking", stopRequest);
        Assert.Equal(HttpStatusCode.OK, stopResponse.StatusCode);

        await using (var db = _fixture.CreateDbContext())
        {
            var session = await db.LocationTrackingSessions.SingleAsync(s => s.Id == sessionId);
            Assert.Equal(TrackingSessionStatus.Stopped, session.Status);
            Assert.NotNull(session.EndedAt);
            Assert.Equal(14.6200m, session.EndLatitude);
            Assert.Equal(120.9950m, session.EndLongitude);
        }
    }

    [SqlServerFact]
    public async Task Driver_cannot_start_tracking_another_drivers_trip()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        using var loginClient = factory.CreateClient();
        var driverToken = await LoginAsync(loginClient, seed.Driver2Username, DefaultPassword);

        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var startRequest = new StartTrackingRequest(14.5995m, 120.9842m);
        var response = await driverClient.PostAsJsonAsync($"/api/driver/trips/{seed.Trip1Id}/start-tracking", startRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Driver_cannot_send_location_after_trip_is_completed()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var trip = await db.DispatchTrips.SingleAsync(t => t.Id == seed.Trip1Id);
            trip.Status = TripStatus.Delivered; 
            await db.SaveChangesAsync();
        }

        using var loginClient = factory.CreateClient();
        var driverToken = await LoginAsync(loginClient, seed.Driver1Username, DefaultPassword);

        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var startRequest = new StartTrackingRequest(14.5995m, 120.9842m);
        var response = await driverClient.PostAsJsonAsync($"/api/driver/trips/{seed.Trip1Id}/start-tracking", startRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Driver_cannot_send_location_without_active_tracking_session()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        using var loginClient = factory.CreateClient();
        var driverToken = await LoginAsync(loginClient, seed.Driver1Username, DefaultPassword);

        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var updateRequest = new SendLocationRequest(
            TrackingSessionId: Guid.NewGuid(),
            TripId: seed.Trip1Id,
            DispatchDriverId: seed.Driver1Id,
            DispatchTruckId: seed.TruckId,
            Latitude: 14.6100m,
            Longitude: 120.9900m,
            AccuracyMeters: 5.2m,
            SpeedKph: 55.4m,
            Heading: 180.0m,
            RecordedAt: DateTime.UtcNow,
            Source: LocationUpdateSource.DriverApp
        );

        var response = await driverClient.PostAsJsonAsync("/api/driver/location", updateRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Invalid_coordinates_are_rejected()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        using var loginClient = factory.CreateClient();
        var driverToken = await LoginAsync(loginClient, seed.Driver1Username, DefaultPassword);

        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var startRequest = new StartTrackingRequest(14.5995m, 120.9842m);
        await driverClient.PostAsJsonAsync($"/api/driver/trips/{seed.Trip1Id}/start-tracking", startRequest);

        var invalidRequest = new SendLocationRequest(
            TrackingSessionId: null,
            TripId: seed.Trip1Id,
            DispatchDriverId: seed.Driver1Id,
            DispatchTruckId: seed.TruckId,
            Latitude: 95.0000m,
            Longitude: 120.9900m,
            AccuracyMeters: 5.2m,
            SpeedKph: 55.4m,
            Heading: 180.0m,
            RecordedAt: DateTime.UtcNow,
            Source: LocationUpdateSource.DriverApp
        );

        var response = await driverClient.PostAsJsonAsync("/api/driver/location", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Live_map_snapshots_and_driver_route_map_security_rules()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString, TestSigningKey);
        var seed = await SeedTrackingDataAsync();

        using var loginClient = factory.CreateClient();
        var dispatcherToken = await LoginAsync(loginClient, seed.DispatcherUsername, DefaultPassword);
        using var dispatcherClient = factory.CreateClient();
        dispatcherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dispatcherToken);

        var dispResponse = await dispatcherClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.OK, dispResponse.StatusCode);
        var dispSnapshot = await dispResponse.Content.ReadFromJsonAsync<LiveMapTripResponse[]>();
        Assert.NotNull(dispSnapshot);
        Assert.True(dispSnapshot.Length >= 2);

        var managerToken = await LoginAsync(loginClient, seed.ManagerUsername, DefaultPassword);
        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        var mgrResponse = await managerClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.OK, mgrResponse.StatusCode);

        var ownerToken = await LoginAsync(loginClient, seed.OwnerUsername, DefaultPassword);
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var ownerResponse = await ownerClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);

        var financeToken = await LoginAsync(loginClient, seed.FinanceUsername, DefaultPassword);
        using var financeClient = factory.CreateClient();
        financeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", financeToken);

        var finResponse = await financeClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.Forbidden, finResponse.StatusCode);

        var inventoryToken = await LoginAsync(loginClient, seed.InventoryUsername, DefaultPassword);
        using var inventoryClient = factory.CreateClient();
        inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", inventoryToken);

        var invResponse = await inventoryClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.Forbidden, invResponse.StatusCode);

        var driverToken = await LoginAsync(loginClient, seed.Driver1Username, DefaultPassword);
        using var driverClient = factory.CreateClient();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var drvResponse = await driverClient.GetAsync("/api/dispatch/live-map");
        Assert.Equal(HttpStatusCode.Forbidden, drvResponse.StatusCode);

        var drvRouteResponse = await driverClient.GetAsync("/api/driver/my-route-map");
        Assert.Equal(HttpStatusCode.OK, drvRouteResponse.StatusCode);
        var drvRoute = await drvRouteResponse.Content.ReadFromJsonAsync<LiveMapTripResponse>();
        Assert.NotNull(drvRoute);
        Assert.Equal(seed.Trip1Id, drvRoute.TripId);

        var mgrRouteResponse = await managerClient.GetAsync("/api/driver/my-route-map");
        Assert.Equal(HttpStatusCode.Forbidden, mgrRouteResponse.StatusCode);
    }

    private async Task<TrackingSeedResult> SeedTrackingDataAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();

        var driver1User = new User { Id = Guid.NewGuid(), Username = $"drv1_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var driver2User = new User { Id = Guid.NewGuid(), Username = $"drv2_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var dispatcherUser = new User { Id = Guid.NewGuid(), Username = $"disp_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var managerUser = new User { Id = Guid.NewGuid(), Username = $"mgr_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var ownerUser = new User { Id = Guid.NewGuid(), Username = $"own_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var financeUser = new User { Id = Guid.NewGuid(), Username = $"fin_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };
        var inventoryUser = new User { Id = Guid.NewGuid(), Username = $"inv_{Guid.NewGuid():N}", PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword), CreatedAt = DateTime.UtcNow, IsActive = true };

        dbContext.Users.AddRange(driver1User, driver2User, dispatcherUser, managerUser, ownerUser, financeUser, inventoryUser);

        var driverRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.Driver).Select(r => r.Id).SingleAsync();
        var dispatcherRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.Dispatcher).Select(r => r.Id).SingleAsync();
        var managerRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.Manager).Select(r => r.Id).SingleAsync();
        var ownerRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.Owner).Select(r => r.Id).SingleAsync();
        var financeRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.HeadOfFinance).Select(r => r.Id).SingleAsync();
        var inventoryRoleId = await dbContext.Roles.Where(r => r.Name == RoleNames.InventoryOfficer).Select(r => r.Id).SingleAsync();

        dbContext.UserRoles.AddRange(
            new UserRole { UserId = driver1User.Id, RoleId = driverRoleId },
            new UserRole { UserId = driver2User.Id, RoleId = driverRoleId },
            new UserRole { UserId = dispatcherUser.Id, RoleId = dispatcherRoleId },
            new UserRole { UserId = managerUser.Id, RoleId = managerRoleId },
            new UserRole { UserId = ownerUser.Id, RoleId = ownerRoleId },
            new UserRole { UserId = financeUser.Id, RoleId = financeRoleId },
            new UserRole { UserId = inventoryUser.Id, RoleId = inventoryRoleId });

        var asset = new Asset { Id = Guid.NewGuid(), AssetCode = $"TRK-{Guid.NewGuid():N}".Substring(0, 20), AssetType = AssetType.Truck, CreatedAt = DateTime.UtcNow, Status = AssetStatus.Active };
        dbContext.Assets.Add(asset);

        var truck = new Truck { Id = Guid.NewGuid(), AssetId = asset.Id, PlateNumber = $"PL-{Guid.NewGuid():N}".Substring(0, 10), ContainerCapability = "20ft", Status = "Active", CreatedAt = DateTime.UtcNow };
        dbContext.DispatchTrucks.Add(truck);

        var driver1 = new Driver { Id = Guid.NewGuid(), UserId = driver1User.Id, LicenseNumber = $"DL1-{Guid.NewGuid():N}".Substring(0, 15), Status = "Active", CreatedAt = DateTime.UtcNow };
        var driver2 = new Driver { Id = Guid.NewGuid(), UserId = driver2User.Id, LicenseNumber = $"DL2-{Guid.NewGuid():N}".Substring(0, 15), Status = "Active", CreatedAt = DateTime.UtcNow };
        dbContext.DispatchDrivers.AddRange(driver1, driver2);

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Tracking Test Customer", CreatedAt = DateTime.UtcNow };
        dbContext.DispatchCustomers.Add(customer);

        var trip1 = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver1User.Id,
            TruckAssetId = asset.Id,
            ContainerSize = "20ft",
            Status = TripStatus.Dispatched,
            CreatedAt = DateTime.UtcNow
        };
        var trip2 = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver2User.Id,
            TruckAssetId = asset.Id,
            ContainerSize = "20ft",
            Status = TripStatus.Dispatched,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DispatchTrips.AddRange(trip1, trip2);
        await dbContext.SaveChangesAsync();

        return new TrackingSeedResult(
            driver1User.Username,
            driver2User.Username,
            trip1.Id,
            trip2.Id,
            driver1.Id,
            driver2.Id,
            truck.Id,
            dispatcherUser.Username,
            managerUser.Username,
            ownerUser.Username,
            financeUser.Username,
            inventoryUser.Username);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("Login did not return an access token.");
        return payload.AccessToken;
    }

    private sealed record TrackingSeedResult(
        string Driver1Username,
        string Driver2Username,
        Guid Trip1Id,
        Guid Trip2Id,
        Guid Driver1Id,
        Guid Driver2Id,
        Guid TruckId,
        string DispatcherUsername,
        string ManagerUsername,
        string OwnerUsername,
        string FinanceUsername,
        string InventoryUsername);
}
