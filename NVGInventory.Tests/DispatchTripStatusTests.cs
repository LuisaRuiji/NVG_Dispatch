using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
public class DispatchTripStatusTests : SqlServerIntegrationTestBase
{
    public DispatchTripStatusTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task FailedAttempt_DriverCannotResolve()
    {
        Guid tripId;
        Guid driverId;
        Guid managerId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, managerId) = await SeedTripAsync(setup, TripStatus.EnroutePickup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.FailedAttempt, "consignee unavailable", null, eventAt, rowVersion),
                driver);

            await Assert.ThrowsAsync<ForbiddenDomainException>(async () =>
            {
                rowVersion = await GetRowVersionAsync(context, tripId);
                await service.ChangeStatusAsync(
                    new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnroutePickup, "resume", null, eventAt.AddMinutes(1), rowVersion),
                    driver);
            });
        }
    }

    [SqlServerFact]
    public async Task DriverCannotSkipStatuses()
    {
        Guid tripId;
        Guid driverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, _) = await SeedTripAsync(setup, TripStatus.Delivered);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            await Assert.ThrowsAsync<ConflictDomainException>(async () =>
            {
                await service.ChangeStatusAsync(
                    new ChangeDispatchTripStatusCommand(tripId, TripStatus.AtPickup, null, null, eventAt, rowVersion),
                    driver);
            });
        }
    }

    [SqlServerFact]
    public async Task DriverCannotSetFailedAttemptFromLoaded()
    {
        Guid tripId;
        Guid driverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, _) = await SeedTripAsync(setup, TripStatus.Loaded);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            await Assert.ThrowsAsync<ConflictDomainException>(async () =>
            {
                await service.ChangeStatusAsync(
                    new ChangeDispatchTripStatusCommand(tripId, TripStatus.FailedAttempt, "delay", null, eventAt, rowVersion),
                    driver);
            });
        }
    }

    [SqlServerFact]
    public async Task FailedAttempt_ManagerCanResumeToPreviousStatus()
    {
        Guid tripId;
        Guid driverId;
        Guid managerId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, managerId) = await SeedTripAsync(setup, TripStatus.EnroutePickup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var manager = new DispatchActorContext(managerId, true, false, false, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.FailedAttempt, "no access", null, eventAt, rowVersion),
                driver);

            rowVersion = await GetRowVersionAsync(context, tripId);
            var resumed = await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnroutePickup, "retry", null, eventAt.AddMinutes(1), rowVersion),
                manager);

            Assert.Equal(TripStatus.EnroutePickup, resumed.Status);
        }
    }

    [SqlServerFact]
    public async Task OnHold_ResumeRequiresManager()
    {
        Guid tripId;
        Guid driverId;
        Guid managerId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, managerId) = await SeedTripAsync(setup, TripStatus.EnroutePickup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var manager = new DispatchActorContext(managerId, true, false, false, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.OnHold, "breakdown", null, eventAt, rowVersion),
                driver);

            await Assert.ThrowsAsync<ForbiddenDomainException>(async () =>
            {
                rowVersion = await GetRowVersionAsync(context, tripId);
                await service.ChangeStatusAsync(
                    new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnroutePickup, "resume", null, eventAt.AddMinutes(1), rowVersion),
                    driver);
            });

            rowVersion = await GetRowVersionAsync(context, tripId);
            var resumed = await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnroutePickup, "resume", null, eventAt.AddMinutes(2), rowVersion),
                manager);

            Assert.Equal(TripStatus.EnroutePickup, resumed.Status);
        }
    }

    [SqlServerFact]
    public async Task Delivered_DoesNotRequirePod()
    {
        Guid tripId;
        Guid driverId;
        Guid managerId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, managerId) = await SeedTripAsync(setup, TripStatus.AtDropoff);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var eventAt = DateTime.UtcNow;
            var rowVersion = await GetRowVersionAsync(context, tripId);

            var delivered = await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.Delivered, null, null, eventAt, rowVersion),
                driver);

            Assert.Equal(TripStatus.Delivered, delivered.Status);
        }
    }

    [SqlServerFact]
    public async Task UploadDocument_CreatesNewVersionAndDeactivatesPrevious()
    {
        Guid tripId;
        Guid driverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, _) = await SeedTripAsync(setup, TripStatus.Delivered);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var documentService = CreateDocumentService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);

            var first = await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "pod/v1.pdf"),
                driver);

            var second = await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "pod/v2.pdf"),
                driver);

            var docs = await context.DispatchTripDocuments
                .Where(d => d.TripId == tripId && d.Type == TripDocumentType.Pod)
                .ToListAsync();

            Assert.Equal(2, docs.Count);

            var active = docs.Single(d => d.IsActive);
            Assert.Equal(second.Id, active.Id);
            Assert.Equal(first.Id, active.SupersedesDocumentId);

            var previous = docs.Single(d => d.Id == first.Id);
            Assert.False(previous.IsActive);
        }
    }

    [SqlServerFact]
    public async Task DriverCannotUploadPodBeforeDelivery()
    {
        Guid tripId;
        Guid driverId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, _) = await SeedTripAsync(setup, TripStatus.AtDropoff);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var documentService = CreateDocumentService(context);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);

            await Assert.ThrowsAsync<ConflictDomainException>(async () =>
            {
                await documentService.UploadDocumentAsync(
                    new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "pod/pre-delivery.pdf"),
                    driver);
            });
        }
    }

    private static DispatchTripService CreateService(InventoryDbContext context, bool docVerificationEnabled = true)
    {
        var options = Options.Create(new DispatchingOptions { DocVerificationEnabled = docVerificationEnabled });
        return new DispatchTripService(
            context,
            new UserService(context),
            new DispatchDocumentWorkflowService(context),
            options,
            new NoOpAuditService());
    }

    private static DispatchDocumentWorkflowService CreateDocumentService(InventoryDbContext context)
    {
        return new DispatchDocumentWorkflowService(context);
    }

    private static async Task<(Guid TripId, Guid DriverId, Guid ManagerId)> SeedTripAsync(
        InventoryDbContext context,
        TripStatus status)
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

        var manager = new User
        {
            Id = Guid.NewGuid(),
            Username = $"manager_{Guid.NewGuid():N}",
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
        context.Users.AddRange(driver, manager);
        context.DispatchTrips.Add(trip);
        await context.SaveChangesAsync();

        return (trip.Id, driver.Id, manager.Id);
    }

    private static async Task<byte[]> GetRowVersionAsync(InventoryDbContext context, Guid tripId)
    {
        var trip = await context.DispatchTrips
            .AsNoTracking()
            .FirstAsync(t => t.Id == tripId);
        return trip.RowVersion;
    }
}
