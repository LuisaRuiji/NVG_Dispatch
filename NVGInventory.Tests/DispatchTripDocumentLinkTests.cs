using System;
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
public class DispatchTripDocumentLinkTests : SqlServerIntegrationTestBase
{
    public DispatchTripDocumentLinkTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task AssignedDriver_CanFetchDocumentLink()
    {
        Guid tripId;
        Guid docId;
        Guid driverId;
        string storageKey;

        await using (var setup = CreateDbContext())
        {
            (tripId, docId, driverId, _, storageKey) = await SeedTripWithDocumentAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(driverId, false, false, true, false, false);

            var result = await service.GetTripDocumentLinkAsync(tripId, docId, actor);
            Assert.Equal(storageKey, result);
        }
    }

    [SqlServerFact]
    public async Task OtherDriver_GetsNotFound()
    {
        Guid tripId;
        Guid docId;
        Guid otherDriverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, docId, _, otherDriverId, _) = await SeedTripWithDocumentAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(otherDriverId, false, false, true, false, false);

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await service.GetTripDocumentLinkAsync(tripId, docId, actor);
            });
        }
    }

    [SqlServerFact]
    public async Task WrongDocId_GetsNotFound()
    {
        Guid tripId;
        Guid driverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, _, driverId, _, _) = await SeedTripWithDocumentAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(driverId, false, false, true, false, false);

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                await service.GetTripDocumentLinkAsync(tripId, Guid.NewGuid(), actor);
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

    private static async Task<(Guid TripId, Guid DocId, Guid DriverId, Guid OtherDriverId, string StorageKey)> SeedTripWithDocumentAsync(
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

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            DriverUserId = driver.Id,
            Status = TripStatus.Dispatched,
            PodPending = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var storageKey = "pod/v1.pdf";
        var doc = new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Type = TripDocumentType.Pod,
            State = TripDocumentState.Uploaded,
            StorageKey = storageKey,
            UploadedByUserId = driver.Id,
            UploadedAt = now,
            IsActive = true
        };

        context.DispatchCustomers.Add(customer);
        context.Users.AddRange(driver, otherDriver);
        context.DispatchTrips.Add(trip);
        context.DispatchTripDocuments.Add(doc);
        await context.SaveChangesAsync();

        return (trip.Id, doc.Id, driver.Id, otherDriver.Id, storageKey);
    }
}
