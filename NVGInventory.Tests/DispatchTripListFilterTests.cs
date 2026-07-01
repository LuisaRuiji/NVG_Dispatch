using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public class DispatchTripListFilterTests : SqlServerIntegrationTestBase
{
    public DispatchTripListFilterTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task GetTrips_FilterByTruck_Works()
    {
        Guid truckA;
        Guid truckB;
        await using (var setup = CreateDbContext())
        {
            (truckA, truckB) = await SeedTripsWithTrucksAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = CreateActor(isManager: true);
            var result = await service.GetTripsAsync(
                null,
                null,
                truckA,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20,
                actor);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item => Assert.Equal(truckA, item.TruckAssetId));
            Assert.All(result.Items, item =>
            {
                Assert.Equal(item.PickupScheduledAt, item.PlannedStart);
                Assert.Equal(item.DropoffScheduledAt, item.PlannedEnd);
                Assert.Equal(60, item.PlannedDurationMinutes);
            });
        }
    }

    [SqlServerFact]
    public async Task GetTrips_FilterByDeliveredRange_Works()
    {
        Guid tripInRange;
        Guid tripOutOfRange;
        var now = DateTime.UtcNow;
        await using (var setup = CreateDbContext())
        {
            (tripInRange, tripOutOfRange) = await SeedDeliveredTripsWithHistoryAsync(setup, now);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = CreateActor(isDispatcher: true);
            var from = now.AddHours(-2);
            var to = now.AddHours(2);

            var result = await service.GetTripsAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                from,
                to,
                null,
                1,
                20,
                actor);

            Assert.Contains(result.Items, item => item.Id == tripInRange);
            Assert.DoesNotContain(result.Items, item => item.Id == tripOutOfRange);
        }
    }

    [SqlServerFact]
    public async Task GetTrips_FilterByPodStatus_Works()
    {
        Guid verifiedTrip;
        Guid pendingTrip;
        await using (var setup = CreateDbContext())
        {
            (verifiedTrip, pendingTrip) = await SeedDeliveredTripsForPodFilterAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var options = new DispatchingOptions
            {
                DocVerificationEnabled = true,
                RequireWaybill = true,
                RequireATW = false
            };
            var service = CreateService(context, options);
            var actor = CreateActor(isManager: true);

            var verifiedResult = await service.GetTripsAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                DispatchPodStatusFilter.Verified,
                1,
                20,
                actor);

            Assert.Contains(verifiedResult.Items, item => item.Id == verifiedTrip);
            Assert.DoesNotContain(verifiedResult.Items, item => item.Id == pendingTrip);
            var verifiedItem = Assert.Single(verifiedResult.Items.Where(item => item.Id == verifiedTrip));
            Assert.Equal(TripDocumentState.Verified, verifiedItem.PodState);
            Assert.True(verifiedItem.CloseDocumentReady);
            Assert.Equal(0, verifiedItem.MissingRequiredDocumentCount);
            Assert.Equal(0, verifiedItem.RejectedRequiredDocumentCount);

            var pendingResult = await service.GetTripsAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                DispatchPodStatusFilter.Pending,
                1,
                20,
                actor);

            Assert.Contains(pendingResult.Items, item => item.Id == pendingTrip);
            Assert.DoesNotContain(pendingResult.Items, item => item.Id == verifiedTrip);
            var pendingItem = Assert.Single(pendingResult.Items.Where(item => item.Id == pendingTrip));
            Assert.Equal(TripDocumentState.Uploaded, pendingItem.PodState);
            Assert.False(pendingItem.CloseDocumentReady);
            Assert.Equal(
                "ATW, EIR, Gate Pass, DR, Waybill, and POD must be complete before closing.",
                pendingItem.CloseDocumentBlockReason);
        }
    }

    [SqlServerFact]
    public async Task GetTrips_CloseReadiness_RequiresFullDocumentSet()
    {
        Guid pendingOverrideTripId;
        Guid blockedTripId;
        await using (var setup = CreateDbContext())
        {
            (pendingOverrideTripId, blockedTripId) = await SeedDeliveredTripsForRelaxedReadinessAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var options = new DispatchingOptions
            {
                DocVerificationEnabled = false,
                RequireWaybill = true,
                RequireATW = false,
                AllowPodPendingOverride = true
            };
            var service = CreateService(context, options);
            var actor = CreateActor(isManager: true);

            var result = await service.GetTripsAsync(
                TripStatus.Delivered,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                20,
                actor);

            var pendingOverrideItem = Assert.Single(result.Items.Where(item => item.Id == pendingOverrideTripId));
            Assert.Equal(TripDocumentState.Missing, pendingOverrideItem.PodState);
            Assert.False(pendingOverrideItem.CloseDocumentReady);
            Assert.Equal(
                "ATW, EIR, Gate Pass, DR, Waybill, and POD must be complete before closing.",
                pendingOverrideItem.CloseDocumentBlockReason);

            var blockedItem = Assert.Single(result.Items.Where(item => item.Id == blockedTripId));
            Assert.Equal(TripDocumentState.Missing, blockedItem.PodState);
            Assert.False(blockedItem.CloseDocumentReady);
            Assert.Equal(
                "ATW, EIR, Gate Pass, DR, Waybill, and POD must be complete before closing.",
                blockedItem.CloseDocumentBlockReason);
        }
    }

    private static DispatchTripQueryService CreateService(InventoryDbContext context, DispatchingOptions? options = null)
    {
        var resolved = options ?? new DispatchingOptions
        {
            DocVerificationEnabled = true,
            RequireWaybill = true,
            RequireATW = false
        };

        return new DispatchTripQueryService(context, Options.Create(resolved));
    }

    private static DispatchActorContext CreateActor(
        bool isManager = false,
        bool isDispatcher = false,
        bool isDriver = false,
        bool isFinance = false,
        bool isCeo = false)
    {
        return new DispatchActorContext(Guid.NewGuid(), isManager, isDispatcher, isDriver, isFinance, isCeo);
    }

    private static async Task<(Guid TruckA, Guid TruckB)> SeedTripsWithTrucksAsync(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Filter Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var truckA = new Asset
        {
            Id = Guid.NewGuid(),
            AssetType = AssetType.Truck,
            AssetCode = "TRK-A",
            Status = AssetStatus.Active,
            CreatedAt = now
        };

        var truckB = new Asset
        {
            Id = Guid.NewGuid(),
            AssetType = AssetType.Truck,
            AssetCode = "TRK-B",
            Status = AssetStatus.Active,
            CreatedAt = now
        };

        var tripA = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            TruckAssetId = truckA.Id,
            Status = TripStatus.Dispatched,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var tripB = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            TruckAssetId = truckB.Id,
            Status = TripStatus.Dispatched,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.Assets.AddRange(truckA, truckB);
        context.DispatchTrips.AddRange(tripA, tripB);
        context.DispatchTripStops.AddRange(CreateStops(tripA.Id, now).Concat(CreateStops(tripB.Id, now)));
        await context.SaveChangesAsync();

        return (truckA.Id, truckB.Id);
    }

    private static async Task<(Guid TripInRange, Guid TripOutOfRange)> SeedDeliveredTripsWithHistoryAsync(
        InventoryDbContext context,
        DateTime baseTime)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Delivered Co",
            CreatedAt = baseTime
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = baseTime
        };

        var tripInRange = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = baseTime,
            UpdatedAt = baseTime
        };

        var tripOutOfRange = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = baseTime.AddMinutes(10),
            UpdatedAt = baseTime.AddMinutes(10)
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.DispatchTrips.AddRange(tripInRange, tripOutOfRange);
        context.DispatchTripStops.AddRange(CreateStops(tripInRange.Id, baseTime).Concat(CreateStops(tripOutOfRange.Id, baseTime)));

        context.DispatchTripStatusHistories.Add(new TripStatusHistory
        {
            Id = Guid.NewGuid(),
            TripId = tripInRange.Id,
            EventType = TripHistoryEventType.StatusChange,
            FromStatus = TripStatus.AtDropoff,
            ToStatus = TripStatus.Delivered,
            ActorUserId = driver.Id,
            EventAt = baseTime,
            RecordedAt = baseTime
        });

        context.DispatchTripStatusHistories.Add(new TripStatusHistory
        {
            Id = Guid.NewGuid(),
            TripId = tripOutOfRange.Id,
            EventType = TripHistoryEventType.StatusChange,
            FromStatus = TripStatus.AtDropoff,
            ToStatus = TripStatus.Delivered,
            ActorUserId = driver.Id,
            EventAt = baseTime.AddDays(-10),
            RecordedAt = baseTime.AddDays(-10)
        });

        await context.SaveChangesAsync();
        return (tripInRange.Id, tripOutOfRange.Id);
    }

    private static async Task<(Guid VerifiedTripId, Guid PendingTripId)> SeedDeliveredTripsForPodFilterAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Pod Filter Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var verifiedTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var pendingTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.DispatchTrips.AddRange(verifiedTrip, pendingTrip);
        context.DispatchTripStops.AddRange(CreateStops(verifiedTrip.Id, now).Concat(CreateStops(pendingTrip.Id, now)));

        AddVerifiedCloseDocuments(context, verifiedTrip.Id, driver.Id, now);
        AddGeneratedWaybill(context, verifiedTrip.Id, driver.Id, now);

        AddVerifiedCloseDocuments(
            context,
            pendingTrip.Id,
            driver.Id,
            now,
            TripDocumentType.Atw,
            TripDocumentType.Eir,
            TripDocumentType.GatePass,
            TripDocumentType.Dr);
        AddGeneratedWaybill(context, pendingTrip.Id, driver.Id, now);

        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = pendingTrip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = "pod/uploaded.pdf",
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            IsActive = true
        });

        await context.SaveChangesAsync();
        return (verifiedTrip.Id, pendingTrip.Id);
    }

    private static void AddVerifiedCloseDocuments(
        InventoryDbContext context,
        Guid tripId,
        Guid actorUserId,
        DateTime now,
        params TripDocumentType[] types)
    {
        var resolvedTypes = types.Length == 0
            ? new[]
            {
                TripDocumentType.Atw,
                TripDocumentType.Eir,
                TripDocumentType.GatePass,
                TripDocumentType.Dr,
                TripDocumentType.Pod
            }
            : types;

        foreach (var type in resolvedTypes)
        {
            context.DispatchTripDocuments.Add(new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = type,
                State = TripDocumentState.Verified,
                StorageKey = $"{type.ToString().ToLowerInvariant()}/verified.pdf",
                UploadedByUserId = actorUserId,
                UploadedAt = now,
                VerifiedByUserId = actorUserId,
                VerifiedAt = now,
                IsActive = true
            });
        }
    }

    private static void AddGeneratedWaybill(
        InventoryDbContext context,
        Guid tripId,
        Guid actorUserId,
        DateTime now)
    {
        context.GeneratedWaybills.Add(new GeneratedWaybill
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            WaybillNumber = $"WB-{Guid.NewGuid():N}"[..30],
            Version = 1,
            GeneratedAt = now,
            GeneratedByUserId = actorUserId,
            WaybillDataJson = "{}",
            IsActive = true
        });
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

    private static async Task<(Guid PendingOverrideTripId, Guid BlockedTripId)> SeedDeliveredTripsForRelaxedReadinessAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Relaxed Readiness Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var pendingOverrideTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var blockedTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.DispatchTrips.AddRange(pendingOverrideTrip, blockedTrip);
        context.DispatchTripStops.AddRange(
            CreateStops(pendingOverrideTrip.Id, now).Concat(CreateStops(blockedTrip.Id, now)));
        await context.SaveChangesAsync();

        return (pendingOverrideTrip.Id, blockedTrip.Id);
    }
}
