using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

public sealed class DispatchTripLocationServiceTests
{
    [Fact]
    public async Task DriverCanRecordLocationForAssignedActiveTrip()
    {
        using var context = CreateContext();
        var (tripId, driverId, _) = SeedTrip(context, TripStatus.EnroutePickup);
        var service = new DispatchTripLocationService(context);

        var result = await service.RecordLocationAsync(
            new RecordTripLocationPingCommand(tripId, 7.0667, 125.6, 12.5, DateTime.UtcNow),
            DriverActor(driverId));

        Assert.Equal(tripId, result.TripId);
        Assert.Equal(driverId, result.DriverId);
        Assert.Equal(1, await context.DispatchTripLocationPings.CountAsync());
    }

    [Fact]
    public async Task DriverCannotRecordLocationForAnotherDriversTrip()
    {
        using var context = CreateContext();
        var (tripId, _, otherDriverId) = SeedTrip(context, TripStatus.EnroutePickup);
        var service = new DispatchTripLocationService(context);

        await Assert.ThrowsAsync<ForbiddenDomainException>(() =>
            service.RecordLocationAsync(
                new RecordTripLocationPingCommand(tripId, 7.0667, 125.6, null, DateTime.UtcNow),
                DriverActor(otherDriverId)));
    }

    [Theory]
    [InlineData(TripStatus.Draft)]
    [InlineData(TripStatus.Delivered)]
    [InlineData(TripStatus.Closed)]
    [InlineData(TripStatus.Cancelled)]
    [InlineData(TripStatus.FailedAttempt)]
    public async Task DriverCannotRecordLocationForInactiveStatuses(TripStatus status)
    {
        using var context = CreateContext();
        var (tripId, driverId, _) = SeedTrip(context, status);
        var service = new DispatchTripLocationService(context);

        await Assert.ThrowsAsync<ConflictDomainException>(() =>
            service.RecordLocationAsync(
                new RecordTripLocationPingCommand(tripId, 7.0667, 125.6, null, DateTime.UtcNow),
                DriverActor(driverId)));
    }

    [Theory]
    [InlineData(-91, 125.6)]
    [InlineData(91, 125.6)]
    [InlineData(7.0667, -181)]
    [InlineData(7.0667, 181)]
    public async Task InvalidCoordinatesAreRejected(double latitude, double longitude)
    {
        using var context = CreateContext();
        var (tripId, driverId, _) = SeedTrip(context, TripStatus.EnroutePickup);
        var service = new DispatchTripLocationService(context);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.RecordLocationAsync(
                new RecordTripLocationPingCommand(tripId, latitude, longitude, null, DateTime.UtcNow),
                DriverActor(driverId)));
    }

    [Fact]
    public async Task TripDetailIncludesLatestDriverLocation()
    {
        using var context = CreateContext();
        var (tripId, driverId, managerId) = SeedTrip(context, TripStatus.EnrouteDropoff);
        var now = DateTime.UtcNow;
        context.DispatchTripLocationPings.AddRange(
            new TripLocationPing
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                DriverId = driverId,
                Latitude = 7.01,
                Longitude = 125.51,
                RecordedAt = now.AddMinutes(-10),
                CreatedAt = now.AddMinutes(-10)
            },
            new TripLocationPing
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                DriverId = driverId,
                Latitude = 7.07,
                Longitude = 125.6,
                AccuracyMeters = 9,
                RecordedAt = now,
                CreatedAt = now
            });
        await context.SaveChangesAsync();

        var queryService = new DispatchTripQueryService(context, Options.Create(new DispatchingOptions()));

        var detail = await queryService.GetTripDetailAsync(
            tripId,
            new DispatchActorContext(managerId, true, false, false, false, false));

        Assert.NotNull(detail.LatestDriverLocation);
        Assert.Equal(7.07, detail.LatestDriverLocation.Latitude);
        Assert.Equal(125.6, detail.LatestDriverLocation.Longitude);
        Assert.False(detail.LatestDriverLocation.IsStale);
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }

    private static DispatchActorContext DriverActor(Guid userId)
    {
        return new DispatchActorContext(userId, false, false, true, false, false);
    }

    private static (Guid TripId, Guid DriverId, Guid OtherDriverId) SeedTrip(
        InventoryDbContext context,
        TripStatus status)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Map Visibility Co",
            CreatedAt = now
        };
        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };
        var otherDriver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = status,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.AddRange(driver, otherDriver);
        context.DispatchTrips.Add(trip);
        context.SaveChanges();

        return (trip.Id, driver.Id, otherDriver.Id);
    }
}
