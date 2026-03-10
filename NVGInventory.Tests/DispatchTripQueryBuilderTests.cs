using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Queries;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

public class DispatchTripQueryBuilderTests
{
    [Fact]
    public void BaseQuery_FiltersToDriver_WhenNotPrivileged()
    {
        using var context = CreateContext();
        var (driverA, driverB) = SeedDriversAndTrips(context);
        var options = new DispatchingOptions();
        var builder = new DispatchTripQueryBuilder(context, options);

        var actor = new DispatchActorContext(driverA, false, false, true, false, false);
        var results = builder.Base(actor).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, trip => Assert.Equal(driverA, trip.DriverUserId));
    }

    [Fact]
    public void BaseQuery_ReturnsAll_WhenPrivileged()
    {
        using var context = CreateContext();
        SeedDriversAndTrips(context);
        var options = new DispatchingOptions();
        var builder = new DispatchTripQueryBuilder(context, options);

        var actor = new DispatchActorContext(Guid.NewGuid(), true, false, false, false, false);
        var results = builder.Base(actor).ToList();

        Assert.True(results.Count >= 2);
    }

    [Fact]
    public void FilterPickupRange_Works()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Pickup Co");
        var driver = SeedDriver(context, "driver_pickup");

        var tripInRange = CreateTrip(context, customer.Id, driver, TripStatus.Dispatched, now);
        var tripOutRange = CreateTrip(context, customer.Id, driver, TripStatus.Dispatched, now.AddDays(1));

        context.DispatchTripStops.AddRange(
            CreateStops(tripInRange.Id, now)
                .Concat(CreateStops(tripOutRange.Id, now.AddDays(2))));
        context.SaveChanges();

        var builder = new DispatchTripQueryBuilder(context, new DispatchingOptions());
        var actor = new DispatchActorContext(driver, false, false, true, false, false);

        var query = builder.FilterPickupRange(builder.Base(actor), now.AddHours(-1), now.AddHours(3));
        var results = query.ToList();

        Assert.Contains(results, trip => trip.Id == tripInRange.Id);
        Assert.DoesNotContain(results, trip => trip.Id == tripOutRange.Id);
    }

    [Fact]
    public void FilterDeliveredRange_Works()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Delivered Co");
        var driver = SeedDriver(context, "driver_delivered");

        var tripInRange = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now);
        var tripOutRange = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now.AddDays(1));

        context.DispatchTripStatusHistories.AddRange(
            new TripStatusHistory
            {
                Id = Guid.NewGuid(),
                TripId = tripInRange.Id,
                EventType = TripHistoryEventType.StatusChange,
                FromStatus = TripStatus.AtDropoff,
                ToStatus = TripStatus.Delivered,
                ActorUserId = driver,
                EventAt = now,
                RecordedAt = now
            },
            new TripStatusHistory
            {
                Id = Guid.NewGuid(),
                TripId = tripOutRange.Id,
                EventType = TripHistoryEventType.StatusChange,
                FromStatus = TripStatus.AtDropoff,
                ToStatus = TripStatus.Delivered,
                ActorUserId = driver,
                EventAt = now.AddDays(-10),
                RecordedAt = now.AddDays(-10)
            });

        context.SaveChanges();

        var builder = new DispatchTripQueryBuilder(context, new DispatchingOptions());
        var actor = new DispatchActorContext(driver, true, false, false, false, false);

        var query = builder.FilterDeliveredRange(builder.Base(actor), now.AddHours(-2), now.AddHours(2));
        var results = query.ToList();

        Assert.Contains(results, trip => trip.Id == tripInRange.Id);
        Assert.DoesNotContain(results, trip => trip.Id == tripOutRange.Id);
    }

    [Fact]
    public void FilterPodStatus_StrictMode_Works()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Pod Strict");
        var driver = SeedDriver(context, "driver_strict");

        var verifiedTrip = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now);
        var pendingTrip = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now.AddMinutes(1));

        context.DispatchTripDocuments.AddRange(
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = verifiedTrip.Id,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Verified,
                StorageKey = "pod/verified.pdf",
                UploadedByUserId = driver,
                UploadedAt = now,
                VerifiedByUserId = driver,
                VerifiedAt = now,
                IsActive = true
            },
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = pendingTrip.Id,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Uploaded,
                StorageKey = "pod/uploaded.pdf",
                UploadedByUserId = driver,
                UploadedAt = now,
                IsActive = true
            });

        context.SaveChanges();

        var options = new DispatchingOptions { DocVerificationEnabled = true };
        var builder = new DispatchTripQueryBuilder(context, options);
        var actor = new DispatchActorContext(driver, true, false, false, false, false);

        var verified = builder.FilterPodStatus(builder.Base(actor), DispatchPodStatusFilter.Verified).ToList();
        Assert.Contains(verified, trip => trip.Id == verifiedTrip.Id);
        Assert.DoesNotContain(verified, trip => trip.Id == pendingTrip.Id);

        var pending = builder.FilterPodStatus(builder.Base(actor), DispatchPodStatusFilter.Pending).ToList();
        Assert.Contains(pending, trip => trip.Id == pendingTrip.Id);
        Assert.DoesNotContain(pending, trip => trip.Id == verifiedTrip.Id);
    }

    [Fact]
    public void FilterPodStatus_RelaxedMode_Works()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Pod Relaxed");
        var driver = SeedDriver(context, "driver_relaxed");

        var uploadedTrip = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now);
        var missingTrip = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now.AddMinutes(1));
        var overrideTrip = CreateTrip(context, customer.Id, driver, TripStatus.Delivered, now.AddMinutes(2), podPending: true);

        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = uploadedTrip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = "pod/uploaded.pdf",
            UploadedByUserId = driver,
            UploadedAt = now,
            IsActive = true
        });

        context.SaveChanges();

        var options = new DispatchingOptions { DocVerificationEnabled = false };
        var builder = new DispatchTripQueryBuilder(context, options);
        var actor = new DispatchActorContext(driver, true, false, false, false, false);

        var pending = builder.FilterPodStatus(builder.Base(actor), DispatchPodStatusFilter.Pending).ToList();
        Assert.Contains(pending, trip => trip.Id == missingTrip.Id);
        Assert.DoesNotContain(pending, trip => trip.Id == uploadedTrip.Id);
        Assert.DoesNotContain(pending, trip => trip.Id == overrideTrip.Id);
    }

    [Fact]
    public void StatusFilters_Work()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Status Co");
        var driver = SeedDriver(context, "driver_status");

        var activeTrip = CreateTrip(context, customer.Id, driver, TripStatus.Dispatched, now);
        var onHoldTrip = CreateTrip(context, customer.Id, driver, TripStatus.OnHold, now.AddMinutes(1));
        var failedTrip = CreateTrip(context, customer.Id, driver, TripStatus.FailedAttempt, now.AddMinutes(2));
        context.SaveChanges();

        var builder = new DispatchTripQueryBuilder(context, new DispatchingOptions());
        var actor = new DispatchActorContext(driver, true, false, false, false, false);

        var active = builder.Active(builder.Base(actor)).ToList();
        Assert.Contains(active, trip => trip.Id == activeTrip.Id);

        var onHold = builder.OnHold(builder.Base(actor)).ToList();
        Assert.Contains(onHold, trip => trip.Id == onHoldTrip.Id);
        Assert.DoesNotContain(onHold, trip => trip.Id == activeTrip.Id);

        var failed = builder.FailedAttempts(builder.Base(actor)).ToList();
        Assert.Contains(failed, trip => trip.Id == failedTrip.Id);
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }

    private static (Guid DriverA, Guid DriverB) SeedDriversAndTrips(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = SeedCustomer(context, "Driver Filter Co");
        var driverA = SeedDriver(context, $"driver_{Guid.NewGuid():N}");
        var driverB = SeedDriver(context, $"driver_{Guid.NewGuid():N}");

        CreateTrip(context, customer.Id, driverA, TripStatus.Dispatched, now);
        CreateTrip(context, customer.Id, driverB, TripStatus.Dispatched, now.AddMinutes(1));

        context.SaveChanges();
        return (driverA, driverB);
    }

    private static Customer SeedCustomer(InventoryDbContext context, string name)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };
        context.DispatchCustomers.Add(customer);
        context.SaveChanges();
        return customer;
    }

    private static Guid SeedDriver(InventoryDbContext context, string username)
    {
        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(driver);
        context.SaveChanges();
        return driver.Id;
    }

    private static Trip CreateTrip(
        InventoryDbContext context,
        Guid customerId,
        Guid driverId,
        TripStatus status,
        DateTime createdAt,
        bool podPending = false)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            DriverUserId = driverId,
            Status = status,
            PodPending = podPending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        context.DispatchTrips.Add(trip);
        return trip;
    }

    private static IEnumerable<TripStop> CreateStops(Guid tripId, DateTime now)
    {
        yield return new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            StopType = TripStopType.Pickup,
            LocationText = "Dock",
            ScheduledAt = now.AddHours(1),
            CreatedAt = now
        };

        yield return new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            StopType = TripStopType.Dropoff,
            LocationText = "Site",
            ScheduledAt = now.AddHours(2),
            CreatedAt = now
        };
    }
}
