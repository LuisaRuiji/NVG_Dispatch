using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

[Collection("SqlServerIntegration")]
public class DispatchTripMonitoringQueryTests : SqlServerIntegrationTestBase
{
    public DispatchTripMonitoringQueryTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task ActiveTrips_ExcludesClosedAndCancelled()
    {
        await using (var setup = CreateDbContext())
        {
            await SeedTripsAsync(setup, TripStatus.Dispatched, TripStatus.Closed, TripStatus.Cancelled);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = CreateActor(isManager: true);
            var result = await service.GetActiveTripsAsync(1, 20, actor);

            Assert.NotEmpty(result.Items);
            Assert.DoesNotContain(result.Items, item => item.Status == TripStatus.Closed);
            Assert.DoesNotContain(result.Items, item => item.Status == TripStatus.Cancelled);
        }
    }

    [SqlServerFact]
    public async Task OnHoldTrips_ReturnsOnlyOnHold()
    {
        await using (var setup = CreateDbContext())
        {
            await SeedTripsAsync(setup, TripStatus.OnHold, TripStatus.Dispatched);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = CreateActor(isDispatcher: true);
            var result = await service.GetOnHoldTripsAsync(1, 20, actor);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item => Assert.Equal(TripStatus.OnHold, item.Status));
        }
    }

    [SqlServerFact]
    public async Task FailedAttemptTrips_ReturnsOnlyFailedAttempts()
    {
        await using (var setup = CreateDbContext())
        {
            await SeedTripsAsync(setup, TripStatus.FailedAttempt, TripStatus.Dispatched);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = CreateActor(isManager: true);
            var result = await service.GetFailedAttemptTripsAsync(1, 20, actor);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item => Assert.Equal(TripStatus.FailedAttempt, item.Status));
        }
    }

    [SqlServerFact]
    public async Task PodPending_StrictMode_FiltersByVerified()
    {
        Guid verifiedTripId;
        Guid uploadedTripId;
        Guid missingTripId;
        await using (var setup = CreateDbContext())
        {
            (verifiedTripId, uploadedTripId, missingTripId) = await SeedDeliveredTripsForPodPendingAsync(setup);
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
            var result = await service.GetPodPendingTripsAsync(1, 20, actor);

            Assert.DoesNotContain(result.Items, item => item.Id == verifiedTripId);
            Assert.Contains(result.Items, item => item.Id == uploadedTripId);
            Assert.Contains(result.Items, item => item.Id == missingTripId);
        }
    }

    [SqlServerFact]
    public async Task PodPending_RelaxedMode_RespectsPodPendingOverrideAndUploads()
    {
        Guid uploadedTripId;
        Guid missingTripId;
        Guid overrideTripId;
        await using (var setup = CreateDbContext())
        {
            (uploadedTripId, missingTripId, overrideTripId) = await SeedDeliveredTripsForRelaxedPendingAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var options = new DispatchingOptions
            {
                DocVerificationEnabled = false,
                RequireWaybill = true,
                RequireATW = false
            };
            var service = CreateService(context, options);
            var actor = CreateActor(isDispatcher: true);
            var result = await service.GetPodPendingTripsAsync(1, 20, actor);

            Assert.DoesNotContain(result.Items, item => item.Id == uploadedTripId);
            Assert.Contains(result.Items, item => item.Id == missingTripId);
            Assert.DoesNotContain(result.Items, item => item.Id == overrideTripId);
        }
    }

    [SqlServerFact]
    public async Task MonitoringAccess_AllowsCeo_BlocksDriver()
    {
        await using (var setup = CreateDbContext())
        {
            await SeedTripsAsync(setup, TripStatus.Dispatched);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var ceoActor = CreateActor(isCeo: true);
            var result = await service.GetActiveTripsAsync(1, 5, ceoActor);
            Assert.NotNull(result);

            var driverActor = CreateActor(isDriver: true);
            await Assert.ThrowsAsync<ForbiddenDomainException>(async () =>
                await service.GetActiveTripsAsync(1, 5, driverActor));
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

    private static async Task SeedTripsAsync(InventoryDbContext context, params TripStatus[] statuses)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Monitor Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);

        foreach (var status in statuses)
        {
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
            context.DispatchTrips.Add(trip);
            context.DispatchTripStops.AddRange(CreateStops(trip.Id, now));
        }

        await context.SaveChangesAsync();
    }

    private static async Task<(Guid VerifiedTripId, Guid UploadedTripId, Guid MissingTripId)> SeedDeliveredTripsForPodPendingAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Pending Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);

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

        var uploadedTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        var missingTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now.AddMinutes(2),
            UpdatedAt = now.AddMinutes(2)
        };

        context.DispatchTrips.AddRange(verifiedTrip, uploadedTrip, missingTrip);
        context.DispatchTripStops.AddRange(
            CreateStops(verifiedTrip.Id, now)
                .Concat(CreateStops(uploadedTrip.Id, now))
                .Concat(CreateStops(missingTrip.Id, now)));

        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = verifiedTrip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Verified,
            StorageKey = "pod/verified.pdf",
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            VerifiedByUserId = driver.Id,
            VerifiedAt = now,
            IsActive = true
        });

        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = uploadedTrip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = "pod/uploaded.pdf",
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            IsActive = true
        });

        await context.SaveChangesAsync();
        return (verifiedTrip.Id, uploadedTrip.Id, missingTrip.Id);
    }

    private static async Task<(Guid UploadedTripId, Guid MissingTripId, Guid OverrideTripId)> SeedDeliveredTripsForRelaxedPendingAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Relaxed Co",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);

        var uploadedTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var missingTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        var overrideTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Delivered,
            PodPending = true,
            CreatedAt = now.AddMinutes(2),
            UpdatedAt = now.AddMinutes(2)
        };

        context.DispatchTrips.AddRange(uploadedTrip, missingTrip, overrideTrip);
        context.DispatchTripStops.AddRange(
            CreateStops(uploadedTrip.Id, now)
                .Concat(CreateStops(missingTrip.Id, now))
                .Concat(CreateStops(overrideTrip.Id, now)));

        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = uploadedTrip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = "pod/uploaded.pdf",
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            IsActive = true
        });

        await context.SaveChangesAsync();
        return (uploadedTrip.Id, missingTrip.Id, overrideTrip.Id);
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
