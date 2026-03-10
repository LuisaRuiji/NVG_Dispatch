using System;
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
public class DispatchTripDocumentVersionsTests : SqlServerIntegrationTestBase
{
    public DispatchTripDocumentVersionsTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task VersionsPaging_ReturnsNewestFirst_WithTotalCount()
    {
        Guid tripId;
        await using (var setup = CreateDbContext())
        {
            (tripId, _) = await SeedTripWithDocumentsAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(Guid.NewGuid(), true, false, false, false, false);

            var page1 = await service.GetTripDocumentVersionsPagedAsync(
                tripId,
                null,
                null,
                null,
                null,
                1,
                2,
                actor);

            var page2 = await service.GetTripDocumentVersionsPagedAsync(
                tripId,
                null,
                null,
                null,
                null,
                2,
                2,
                actor);

            Assert.Equal(3, page1.TotalCount);
            Assert.Equal(2, page1.Items.Count);
            Assert.Single(page2.Items);

            var ordered = page1.Items.Concat(page2.Items).ToList();
            var uploadedAt = ordered.Select(d => d.UploadedAt).ToList();
            var sorted = uploadedAt.OrderByDescending(x => x).ToList();
            Assert.Equal(sorted, uploadedAt);
        }
    }

    [SqlServerFact]
    public async Task VersionsFilter_ByType_Works()
    {
        Guid tripId;
        await using (var setup = CreateDbContext())
        {
            (tripId, _) = await SeedTripWithDocumentsAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(Guid.NewGuid(), true, false, false, false, false);

            var result = await service.GetTripDocumentVersionsPagedAsync(
                tripId,
                TripDocumentType.Pod,
                null,
                null,
                null,
                1,
                10,
                actor);

            Assert.All(result.Items, item => Assert.Equal(TripDocumentType.Pod, item.Type));
        }
    }

    [SqlServerFact]
    public async Task VersionsFilter_ByState_Works()
    {
        Guid tripId;
        await using (var setup = CreateDbContext())
        {
            (tripId, _) = await SeedTripWithDocumentsAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(Guid.NewGuid(), true, false, false, false, false);

            var result = await service.GetTripDocumentVersionsPagedAsync(
                tripId,
                null,
                TripDocumentState.Verified,
                null,
                null,
                1,
                10,
                actor);

            Assert.All(result.Items, item => Assert.Equal(TripDocumentState.Verified, item.State));
        }
    }

    [SqlServerFact]
    public async Task VersionsFilter_ByDateRange_Inclusive()
    {
        Guid tripId;
        DateTime from;
        DateTime to;

        await using (var setup = CreateDbContext())
        {
            (tripId, _, from, to) = await SeedTripWithDocumentsForDateRangeAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(Guid.NewGuid(), true, false, false, false, false);

            var result = await service.GetTripDocumentVersionsPagedAsync(
                tripId,
                null,
                null,
                from,
                to,
                1,
                10,
                actor);

            Assert.NotEmpty(result.Items);
            Assert.All(result.Items, item =>
            {
                Assert.True(item.UploadedAt >= from);
                Assert.True(item.UploadedAt <= to);
            });
        }
    }

    [SqlServerFact]
    public async Task DriverCannotAccessOtherDriversTripVersions()
    {
        Guid tripId;
        Guid driverId;
        Guid otherDriverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, otherDriverId) = await SeedTripForAccessAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(otherDriverId, false, false, true, false, false);

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await service.GetTripDocumentVersionsPagedAsync(
                    tripId,
                    null,
                    null,
                    null,
                    null,
                    1,
                    10,
                    actor);
            });
        }
    }

    private static DispatchTripQueryService CreateService(InventoryDbContext context)
    {
        var options = new DispatchingOptions
        {
            DocVerificationEnabled = true,
            RequireWaybill = true,
            RequireATW = false
        };

        return new DispatchTripQueryService(context, Options.Create(options));
    }

    private static async Task<(Guid TripId, Guid DriverId)> SeedTripWithDocumentsAsync(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var (tripId, driverId) = await SeedTripAsync(context);

        context.DispatchTripDocuments.AddRange(
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Uploaded,
                StorageKey = "pod/v1.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-50),
                IsActive = true
            },
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Waybill,
                State = TripDocumentState.Verified,
                StorageKey = "waybill/v1.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-30),
                VerifiedByUserId = driverId,
                VerifiedAt = now.AddMinutes(-29),
                IsActive = true
            },
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Rejected,
                StorageKey = "pod/v2.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-10),
                RejectedByUserId = driverId,
                RejectedAt = now.AddMinutes(-9),
                IsActive = true
            });

        await context.SaveChangesAsync();
        return (tripId, driverId);
    }

    private static async Task<(Guid TripId, Guid DriverId, DateTime From, DateTime To)> SeedTripWithDocumentsForDateRangeAsync(
        InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var (tripId, driverId) = await SeedTripAsync(context);

        context.DispatchTripDocuments.AddRange(
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Uploaded,
                StorageKey = "pod/old.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-120),
                IsActive = true
            },
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Pod,
                State = TripDocumentState.Uploaded,
                StorageKey = "pod/in-range.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-40),
                IsActive = true
            },
            new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                Type = TripDocumentType.Waybill,
                State = TripDocumentState.Verified,
                StorageKey = "waybill/new.pdf",
                UploadedByUserId = driverId,
                UploadedAt = now.AddMinutes(-5),
                IsActive = true
            });

        await context.SaveChangesAsync();
        return (tripId, driverId, now.AddMinutes(-45), now.AddMinutes(-5));
    }

    private static async Task<(Guid TripId, Guid DriverId, Guid OtherDriverId)> SeedTripForAccessAsync(InventoryDbContext context)
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

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Dispatched,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.AddRange(driver, otherDriver);
        context.DispatchTrips.Add(trip);
        context.DispatchTripDocuments.Add(new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = "pod/driver.pdf",
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            IsActive = true
        });

        await context.SaveChangesAsync();
        return (trip.Id, driver.Id, otherDriver.Id);
    }

    private static async Task<(Guid TripId, Guid DriverId)> SeedTripAsync(InventoryDbContext context)
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

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Dispatched,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(driver);
        context.DispatchTrips.Add(trip);
        await context.SaveChangesAsync();

        return (trip.Id, driver.Id);
    }
}
