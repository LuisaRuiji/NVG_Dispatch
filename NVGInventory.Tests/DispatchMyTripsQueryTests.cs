using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public class DispatchMyTripsQueryTests : SqlServerIntegrationTestBase
{
    public DispatchMyTripsQueryTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task MyTripDetail_ReturnsNotFound_ForOtherDriver()
    {
        Guid tripId;
        Guid driverId;
        Guid otherDriverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, otherDriverId) = await SeedDriverTripsAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await service.GetMyTripDetailAsync(tripId, driverId);
            });
        }
    }

    [SqlServerFact]
    public async Task MyTrips_ReturnsOnlyCurrentDriverTrips()
    {
        Guid tripId;
        Guid driverId;
        Guid otherDriverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, otherDriverId) = await SeedDriverTripsAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var result = await service.GetMyTripsAsync(driverId, includeClosed: true, null, null, 1, 20);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item => Assert.Equal(driverId, item.DriverUserId));
        }
    }

    [SqlServerFact]
    public async Task MyTrips_Paging_Works()
    {
        Guid driverId;
        await using (var setup = CreateDbContext())
        {
            driverId = await SeedTripsForPagingAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var page1 = await service.GetMyTripsAsync(driverId, includeClosed: true, null, null, 1, 2);
            var page2 = await service.GetMyTripsAsync(driverId, includeClosed: true, null, null, 2, 2);

            Assert.Equal(3, page1.TotalCount);
            Assert.Equal(2, page1.Items.Count);
            Assert.Single(page2.Items);
        }
    }

    [SqlServerFact]
    public async Task MyTrips_ActiveScope_ExcludesClosedAndCancelled()
    {
        Guid driverId;
        await using (var setup = CreateDbContext())
        {
            driverId = await SeedTripsForActiveScopeAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var result = await service.GetMyTripsAsync(driverId, includeClosed: false, null, null, 1, 20);

            Assert.NotEmpty(result.Items);
            Assert.DoesNotContain(result.Items, item => item.Status == TripStatus.Closed);
            Assert.DoesNotContain(result.Items, item => item.Status == TripStatus.Cancelled);
        }
    }

    [SqlServerFact]
    public async Task RequiredDocumentCount_AlwaysIncludesPod()
    {
        Guid driverId;
        await using (var setup = CreateDbContext())
        {
            driverId = await SeedSingleTripAsync(setup, TripStatus.Dispatched);
        }

        await using (var context = CreateDbContext())
        {
            var options = new DispatchingOptions
            {
                DocVerificationEnabled = true,
                RequireWaybill = false,
                RequireATW = false
            };
            var service = CreateService(context, options);

            var result = await service.GetMyTripsAsync(driverId, includeClosed: true, null, null, 1, 10);
            var item = Assert.Single(result.Items);

            Assert.Equal(1, item.RequiredDocumentCount);
            Assert.Contains(item.Documents, doc => doc.Type == TripDocumentType.Pod);
        }
    }

    [SqlServerFact]
    public async Task UploadedDocumentCount_ExcludesRejectedAndInactive()
    {
        Guid tripId;
        Guid driverId;
        await using (var setup = CreateDbContext())
        {
            (tripId, driverId) = await SeedTripWithDriverAsync(setup, TripStatus.Dispatched);

            setup.DispatchTripDocuments.AddRange(
                new TripDocument
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    Type = TripDocumentType.Pod,
                    State = TripDocumentState.Uploaded,
                    StorageKey = "pod/v1.pdf",
                    UploadedByUserId = driverId,
                    UploadedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new TripDocument
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    Type = TripDocumentType.Waybill,
                    State = TripDocumentState.Rejected,
                    StorageKey = "waybill/v1.pdf",
                    UploadedByUserId = driverId,
                    UploadedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new TripDocument
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    Type = TripDocumentType.Pod,
                    State = TripDocumentState.Uploaded,
                    StorageKey = "pod/old.pdf",
                    UploadedByUserId = driverId,
                    UploadedAt = DateTime.UtcNow.AddMinutes(-10),
                    IsActive = false
                });

            await setup.SaveChangesAsync();
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
            var result = await service.GetMyTripsAsync(driverId, includeClosed: true, null, null, 1, 10);

            var item = Assert.Single(result.Items);
            Assert.Equal(1, item.UploadedDocumentCount);
        }
    }

    [SqlServerFact]
    public async Task UploadedDocumentCount_UsesOnlyActiveVersion()
    {
        Guid tripId;
        Guid driverId;
        await using (var setup = CreateDbContext())
        {
            (tripId, driverId) = await SeedTripWithDriverAsync(setup, TripStatus.Delivered);
        }

        await using (var context = CreateDbContext())
        {
            var options = new DispatchingOptions
            {
                DocVerificationEnabled = true,
                RequireWaybill = false,
                RequireATW = false
            };
            var documentService = new DispatchDocumentWorkflowService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);

            await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "pod/v1.pdf"),
                driver);

            await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "pod/v2.pdf"),
                driver);

            var queryService = CreateService(context, options);
            var result = await queryService.GetMyTripsAsync(driverId, includeClosed: true, null, null, 1, 10);
            var item = Assert.Single(result.Items);

            Assert.Equal(1, item.UploadedDocumentCount);
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

    private static async Task<(Guid TripId, Guid DriverId, Guid OtherDriverId)> SeedDriverTripsAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Acme Logistics",
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

        var otherTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = otherDriver.Id,
            Status = TripStatus.Dispatched,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.AddRange(driver, otherDriver);
        context.DispatchTrips.Add(otherTrip);
        context.DispatchTripStops.AddRange(CreateStops(otherTrip.Id, now));

        var driverTrip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Dispatched,
            PodPending = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };

        context.DispatchTrips.Add(driverTrip);
        context.DispatchTripStops.AddRange(CreateStops(driverTrip.Id, now.AddMinutes(1)));
        await context.SaveChangesAsync();

        return (otherTrip.Id, driver.Id, otherDriver.Id);
    }

    private static async Task<Guid> SeedTripsForPagingAsync(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Omega Logistics",
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

        var trips = new List<Trip>();
        for (var i = 0; i < 3; i++)
        {
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DriverUserId = driver.Id,
                Status = TripStatus.Dispatched,
                PodPending = false,
                CreatedAt = now.AddMinutes(i),
                UpdatedAt = now.AddMinutes(i)
            };
            trips.Add(trip);
        }

        context.DispatchTrips.AddRange(trips);
        foreach (var trip in trips)
        {
            context.DispatchTripStops.AddRange(CreateStops(trip.Id, now));
        }

        await context.SaveChangesAsync();
        return driver.Id;
    }

    private static async Task<Guid> SeedTripsForActiveScopeAsync(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Atlas Freight",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var trips = new[]
        {
            new Trip
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DriverUserId = driver.Id,
                Status = TripStatus.Dispatched,
                PodPending = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Trip
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DriverUserId = driver.Id,
                Status = TripStatus.Closed,
                PodPending = false,
                CreatedAt = now.AddMinutes(1),
                UpdatedAt = now.AddMinutes(1)
            },
            new Trip
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                DriverUserId = driver.Id,
                Status = TripStatus.Cancelled,
                PodPending = false,
                CreatedAt = now.AddMinutes(2),
                UpdatedAt = now.AddMinutes(2)
            }
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.DispatchTrips.AddRange(trips);
        foreach (var trip in trips)
        {
            context.DispatchTripStops.AddRange(CreateStops(trip.Id, now));
        }

        await context.SaveChangesAsync();
        return driver.Id;
    }

    private static IEnumerable<TripStop> CreateStops(Guid tripId, DateTime now)
    {
        yield return new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            StopType = TripStopType.Pickup,
            LocationText = "Warehouse",
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

    private static async Task<Guid> SeedSingleTripAsync(InventoryDbContext context, TripStatus status)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Doc Count Co",
            CreatedAt = now
        };

        var driver = new User
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
        context.Users.Add(driver);
        context.DispatchTrips.Add(trip);
        context.DispatchTripStops.AddRange(CreateStops(trip.Id, now));
        await context.SaveChangesAsync();

        return driver.Id;
    }

    private static async Task<(Guid TripId, Guid DriverId)> SeedTripWithDriverAsync(
        InventoryDbContext context,
        TripStatus status)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Docs Only Co",
            CreatedAt = now
        };

        var driver = new User
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
        context.Users.Add(driver);
        context.DispatchTrips.Add(trip);
        context.DispatchTripStops.AddRange(CreateStops(trip.Id, now));
        await context.SaveChangesAsync();

        return (trip.Id, driver.Id);
    }
}
