using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Domain.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public class DispatchTripSimulationTests : SqlServerIntegrationTestBase
{
    public DispatchTripSimulationTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task FullTripSimulation_CompletesLifecycle()
    {
        Guid tripId;
        Guid driverId;
        Guid dispatcherId;
        Guid managerId;
        Guid financeId;
        Guid truckId;

        await using (var setup = CreateDbContext())
        {
            (tripId, driverId, dispatcherId, managerId, financeId, truckId) = await SeedDraftTripAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var documentService = CreateDocumentService(context);
            var dispatcher = new DispatchActorContext(dispatcherId, false, true, false, false, false);
            var driver = new DispatchActorContext(driverId, false, false, true, false, false);
            var manager = new DispatchActorContext(managerId, true, false, false, false, false);
            var finance = new DispatchActorContext(financeId, false, false, false, true, false);

            var createdAt = await context.DispatchTrips
                .AsNoTracking()
                .Where(t => t.Id == tripId)
                .Select(t => t.CreatedAt)
                .FirstAsync();
            DateTime? lastEventAt = null;

            DateTime NextEventAt()
            {
                var candidate = DateTime.UtcNow;
                if (candidate < createdAt)
                {
                    candidate = createdAt.AddSeconds(1);
                }

                if (lastEventAt.HasValue && candidate <= lastEventAt.Value)
                {
                    candidate = lastEventAt.Value.AddSeconds(1);
                }

                lastEventAt = candidate;
                return candidate;
            }

            var rowVersion = await GetRowVersionAsync(context, tripId);
            await service.DispatchAsync(
                new DispatchTripCommand(tripId, driverId, truckId, "dispatching", rowVersion),
                dispatcher);

            var eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnroutePickup, null, null, eventAt, rowVersion),
                driver);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.AtPickup, null, null, eventAt, rowVersion),
                driver);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.Loaded, null, null, eventAt, rowVersion),
                driver);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.EnrouteDropoff, null, null, eventAt, rowVersion),
                driver);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.AtDropoff, null, null, eventAt, rowVersion),
                driver);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.Delivered, null, null, eventAt, rowVersion),
                driver);

            await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Waybill, "docs/waybill.pdf"),
                driver);
            await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Atw, "docs/atw.pdf"),
                driver);
            var podDoc = await documentService.UploadDocumentAsync(
                new UploadTripDocumentCommand(tripId, TripDocumentType.Pod, "docs/pod.pdf"),
                driver);

            await documentService.VerifyDocumentAsync(new VerifyTripDocumentCommand(tripId, podDoc.Id), finance);

            eventAt = NextEventAt();
            rowVersion = await GetRowVersionAsync(context, tripId);
            var closed = await service.ChangeStatusAsync(
                new ChangeDispatchTripStatusCommand(tripId, TripStatus.Closed, "close", null, eventAt, rowVersion),
                manager);

            Assert.Equal(TripStatus.Closed, closed.Status);
            var trip = await context.DispatchTrips.AsNoTracking().FirstAsync(t => t.Id == tripId);
            Assert.Equal(TripStatus.Closed, trip.Status);
        }
    }

    [SqlServerFact]
    public async Task CreateDraft_RejectsPickupAfterDropoff()
    {
        Guid customerId;
        Guid dispatcherId;
        var now = DateTime.UtcNow;

        await using (var setup = CreateDbContext())
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Window Validation Co",
                CreatedAt = now
            };

            var dispatcher = new User
            {
                Id = Guid.NewGuid(),
                Username = $"dispatcher_{Guid.NewGuid():N}",
                PasswordHash = TestPasswords.Hashed,
                CreatedAt = now
            };

            setup.DispatchCustomers.Add(customer);
            setup.Users.Add(dispatcher);
            await setup.SaveChangesAsync();

            customerId = customer.Id;
            dispatcherId = dispatcher.Id;
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var actor = new DispatchActorContext(dispatcherId, false, true, false, false, false);
            var command = new CreateDispatchTripCommand(
                customerId,
                null,
                null,
                null,
                new[]
                {
                    new DispatchTripStopInput(TripStopType.Pickup, "Dock A", now.AddHours(4)),
                    new DispatchTripStopInput(TripStopType.Dropoff, "Dock B", now.AddHours(2))
                });

            var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(async () =>
                await service.CreateDraftAsync(command, actor));

            Assert.Equal("Pickup time must be earlier than dropoff time.", ex.Message);
        }
    }

    [SqlServerFact]
    public async Task Dispatch_ConflictMessage_IncludesTripAssignmentAndWindow()
    {
        Guid firstTripId;
        Guid driverId;
        Guid dispatcherId;
        Guid truckId;

        await using (var setup = CreateDbContext())
        {
            (firstTripId, driverId, dispatcherId, _, _, truckId) = await SeedDraftTripAsync(setup);
        }

        await using (var context = CreateDbContext())
        {
            var service = CreateService(context);
            var dispatcher = new DispatchActorContext(dispatcherId, false, true, false, false, false);

            var firstRowVersion = await GetRowVersionAsync(context, firstTripId);
            await service.DispatchAsync(
                new DispatchTripCommand(firstTripId, driverId, truckId, "dispatch-first", firstRowVersion),
                dispatcher);

            var firstTripWindow = await context.DispatchTripStops
                .AsNoTracking()
                .Where(stop => stop.TripId == firstTripId)
                .ToListAsync();
            var firstPickup = firstTripWindow.First(stop => stop.StopType == TripStopType.Pickup).ScheduledAt!.Value;
            var firstDropoff = firstTripWindow.First(stop => stop.StopType == TripStopType.Dropoff).ScheduledAt!.Value;

            var customerId = await context.DispatchTrips
                .AsNoTracking()
                .Where(trip => trip.Id == firstTripId)
                .Select(trip => trip.CustomerId)
                .FirstAsync();

            var secondTrip = await service.CreateDraftAsync(
                new CreateDispatchTripCommand(
                    customerId,
                    null,
                    null,
                    "overlap draft",
                    new[]
                    {
                        new DispatchTripStopInput(TripStopType.Pickup, "Dock C", firstPickup.AddMinutes(30)),
                        new DispatchTripStopInput(TripStopType.Dropoff, "Dock D", firstDropoff.AddMinutes(30))
                    }),
                dispatcher);

            var secondRowVersion = await GetRowVersionAsync(context, secondTrip.Id);
            var ex = await Assert.ThrowsAsync<ConflictDomainException>(async () =>
                await service.DispatchAsync(
                    new DispatchTripCommand(secondTrip.Id, driverId, truckId, "dispatch-second", secondRowVersion),
                    dispatcher));

            Assert.Contains("already assigned", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Driver", ex.Message, StringComparison.OrdinalIgnoreCase);

            var detailsJson = JsonSerializer.Serialize(ex.Details);
            Assert.Contains(firstTripId.ToString(), detailsJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("truckAssetCode", detailsJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("windowStart", detailsJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DispatchTripService CreateService(InventoryDbContext context)
    {
        var options = Options.Create(new DispatchingOptions
        {
            DocVerificationEnabled = true,
            RequireWaybill = true,
            RequireATW = false
        });
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

    private static async Task<(Guid TripId, Guid DriverId, Guid DispatcherId, Guid ManagerId, Guid FinanceId, Guid TruckId)>
        SeedDraftTripAsync(InventoryDbContext context)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Simulation Customer",
            CreatedAt = now
        };

        var driver = new User
        {
            Id = Guid.NewGuid(),
            Username = $"driver_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var dispatcher = new User
        {
            Id = Guid.NewGuid(),
            Username = $"dispatcher_{Guid.NewGuid():N}",
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

        var finance = new User
        {
            Id = Guid.NewGuid(),
            Username = $"finance_{Guid.NewGuid():N}",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };

        var truck = new Asset
        {
            Id = Guid.NewGuid(),
            AssetType = AssetType.Truck,
            AssetCode = "TRK-SIM",
            Status = AssetStatus.Active,
            CreatedAt = now
        };

        context.DispatchCustomers.Add(customer);
        context.Users.AddRange(driver, dispatcher, manager, finance);
        context.Assets.Add(truck);
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, driver.Id, RoleNames.Driver);
        await AssignRoleAsync(context, finance.Id, RoleNames.HeadOfFinance);

        var service = CreateService(context);
        var pickupAt = now.AddHours(1);
        var dropoffAt = now.AddHours(5);
        var command = new CreateDispatchTripCommand(
            customer.Id,
            null,
            null,
            null,
            new[]
            {
                new DispatchTripStopInput(TripStopType.Pickup, "Dock A", pickupAt),
                new DispatchTripStopInput(TripStopType.Dropoff, "Dock B", dropoffAt)
            });

        var actor = new DispatchActorContext(dispatcher.Id, false, true, false, false, false);
        var trip = await service.CreateDraftAsync(command, actor);

        return (trip.Id, driver.Id, dispatcher.Id, manager.Id, finance.Id, truck.Id);
    }

    private static async Task AssignRoleAsync(InventoryDbContext context, Guid userId, string roleName)
    {
        var role = await context.Roles.FirstAsync(r => r.Name == roleName);
        var exists = await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id);
        if (!exists)
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.Id
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task<byte[]> GetRowVersionAsync(InventoryDbContext context, Guid tripId)
    {
        var trip = await context.DispatchTrips
            .AsNoTracking()
            .FirstAsync(t => t.Id == tripId);
        return trip.RowVersion;
    }
}
