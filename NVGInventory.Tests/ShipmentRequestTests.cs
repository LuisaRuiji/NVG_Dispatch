using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Modules.ShipmentRequests.Services;
using Xunit;

namespace NVGInventory.Tests;

[Collection("SqlServerIntegration")]
public class ShipmentRequestTests : SqlServerIntegrationTestBase
{
    public ShipmentRequestTests(SqlServerIntegrationFixture fixture) : base(fixture)
    {
    }

    [SqlServerFact]
    public async Task CustomerCannotAccessOtherCustomerRequest()
    {
        await using var context = CreateDbContext();
        var now = DateTime.UtcNow;

        var customerA = new Customer { Id = Guid.NewGuid(), Name = "Customer A", CreatedAt = now };
        var customerB = new Customer { Id = Guid.NewGuid(), Name = "Customer B", CreatedAt = now };
        var userA = new User
        {
            Id = Guid.NewGuid(),
            Username = "customer_a",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now,
            CustomerId = customerA.Id
        };
        var userB = new User
        {
            Id = Guid.NewGuid(),
            Username = "customer_b",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now,
            CustomerId = customerB.Id
        };

        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customerA.Id,
            Status = ShipmentRequestStatus.Draft,
            PickupLocation = "Dock A",
            DropoffLocation = "Dock B",
            RequestedPickupTime = now.AddDays(1),
            CreatedAt = now,
            CreatedByUserId = userA.Id
        };

        context.DispatchCustomers.AddRange(customerA, customerB);
        context.Users.AddRange(userA, userB);
        context.ShipmentRequests.Add(request);
        await context.SaveChangesAsync();

        var queryService = new ShipmentRequestQueryService(
            context,
            new DispatchShipmentReadService(context));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            queryService.GetCustomerRequestDetailAsync(request.Id, customerB.Id));
    }

    [SqlServerFact]
    public async Task CustomerCannotEditSubmittedRequest()
    {
        await using var context = CreateDbContext();
        var now = DateTime.UtcNow;

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Customer A", CreatedAt = now };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "customer_edit",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now,
            CustomerId = customer.Id
        };
        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = ShipmentRequestStatus.Submitted,
            PickupLocation = "Dock A",
            DropoffLocation = "Dock B",
            RequestedPickupTime = now.AddDays(1),
            CreatedAt = now,
            CreatedByUserId = user.Id
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(user);
        context.ShipmentRequests.Add(request);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<ConflictDomainException>(() =>
            service.UpdateDraftAsync(request.Id, new UpdateShipmentRequestCommand(
                customer.Id,
                "Dock A1",
                "Dock B1",
                now.AddDays(2),
                "Updated cargo",
                100m,
                "Handle with care",
                user.Id)));
    }

    [SqlServerFact]
    public async Task DispatcherCanApproveAndReject()
    {
        await using var context = CreateDbContext();
        var now = DateTime.UtcNow;

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Customer A", CreatedAt = now };
        var dispatcher = new User
        {
            Id = Guid.NewGuid(),
            Username = "dispatcher_1",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };
        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = ShipmentRequestStatus.Submitted,
            PickupLocation = "Dock A",
            DropoffLocation = "Dock B",
            RequestedPickupTime = now.AddDays(1),
            CreatedAt = now,
            CreatedByUserId = dispatcher.Id
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(dispatcher);
        context.ShipmentRequests.Add(request);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var approved = await service.ApproveAsync(request.Id, dispatcher.Id);
        Assert.Equal(ShipmentRequestStatus.Approved, approved.Status);
        Assert.Equal(dispatcher.Id, approved.ApprovedByUserId);
        Assert.NotNull(approved.ApprovedAt);

        approved.Status = ShipmentRequestStatus.Submitted;
        approved.ApprovedByUserId = null;
        approved.ApprovedAt = null;
        await context.SaveChangesAsync();

        var rejected = await service.RejectAsync(request.Id, dispatcher.Id, "missing docs");
        Assert.Equal(ShipmentRequestStatus.Rejected, rejected.Status);
    }

    [SqlServerFact]
    public async Task ConvertCreatesTripAndLinksRequest()
    {
        await using var context = CreateDbContext();
        var now = DateTime.UtcNow;

        var customer = new Customer { Id = Guid.NewGuid(), Name = "Customer A", CreatedAt = now };
        var dispatcher = new User
        {
            Id = Guid.NewGuid(),
            Username = "dispatcher_convert",
            PasswordHash = TestPasswords.Hashed,
            CreatedAt = now
        };
        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = ShipmentRequestStatus.Approved,
            PickupLocation = "Dock A",
            DropoffLocation = "Dock B",
            RequestedPickupTime = now.AddDays(1),
            CreatedAt = now,
            CreatedByUserId = dispatcher.Id,
            ApprovedAt = now,
            ApprovedByUserId = dispatcher.Id
        };

        context.DispatchCustomers.Add(customer);
        context.Users.Add(dispatcher);
        context.ShipmentRequests.Add(request);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var actor = new DispatchActorContext(dispatcher.Id, false, true, false, false, false);
        var result = await service.ConvertToTripAsync(request.Id, actor);

        var updated = await context.ShipmentRequests.AsNoTracking().FirstAsync(r => r.Id == request.Id);
        var trip = await context.DispatchTrips.AsNoTracking().FirstAsync(t => t.Id == result.TripId);

        Assert.Equal(ShipmentRequestStatus.ConvertedToTrip, updated.Status);
        Assert.Equal(trip.Id, updated.ConvertedTripId);
        Assert.Equal(customer.Id, trip.CustomerId);
        Assert.Equal(TripStatus.Draft, trip.Status);
    }

    private static ShipmentRequestService CreateService(InventoryDbContext context)
    {
        var options = Options.Create(new DispatchingOptions());
        var userService = new UserService(context);
        var tripLifecycleService = new DispatchTripService(
            context,
            userService,
            new DispatchDocumentWorkflowService(context),
            options,
            new NoOpAuditService());
        var tripDispatchGateway = new DispatchShipmentRequestTripDispatchGateway(tripLifecycleService);
        var tripCreationService = new ShipmentRequestTripCreationService(context, tripDispatchGateway);
        return new ShipmentRequestService(context, userService, tripCreationService);
    }
}
