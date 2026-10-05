using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Controllers;
using NVGInventory.Modules.Customers.Controllers;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Modules.ShipmentRequests.Services;
using NVGInventory.Modules.ShipmentRequests;
using Xunit;

namespace NVGInventory.Tests;

public sealed class ControlledDispatchLifecycleTests
{
    [Fact]
    public void PendingAccount_CannotSubmitBooking()
    {
        var customer = new Customer { AccountStatus = CustomerAccountStatus.PendingReview };
        Assert.Throws<ForbiddenDomainException>(() => CustomerAccountService.EnsureCanSubmitBooking(customer));
    }

    [Fact]
    public async Task PrepaidBooking_CannotConvertBeforeVerifiedPayment()
    {
        await using var db = CreateDbContext();
        var request = CreateBooking(CustomerAccountStatus.ActivePrepaid, BookingFinanceClearanceStatus.AwaitingPayment);
        db.ShipmentRequests.Add(request);
        await db.SaveChangesAsync();
        var gateway = new RecordingTripGateway();
        var service = new ShipmentRequestTripCreationService(db, gateway);

        await Assert.ThrowsAsync<ConflictDomainException>(() => service.CreateDraftTripFromApprovedRequestAsync(
            new CreateTripFromShipmentRequestCommand(request.Id), Actor(dispatcher: true)));

        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task CreditBooking_WithOverdueBalance_IsBlockedAndAudited()
    {
        await using var db = CreateDbContext();
        var request = CreateBooking(CustomerAccountStatus.ActiveCredit, BookingFinanceClearanceStatus.AwaitingCreditReview);
        request.Customer!.CreditStatus = CustomerCreditStatus.Approved;
        request.Customer.CreditLimit = 100_000m;
        request.Customer.HasOverdueBalance = true;
        db.ShipmentRequests.Add(request);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditService();
        var service = new BookingFinanceService(db, audit);

        await Assert.ThrowsAsync<ConflictDomainException>(() => service.ClearCreditAsync(
            request.Id, new ClearCreditBookingCommand(5_000m, "Reviewed current aging report."), Actor(finance: true)));

        Assert.Equal(BookingFinanceClearanceStatus.Blocked, request.FinanceClearanceStatus);
        Assert.Contains(db.BookingFinanceHistories, item => item.ToStatus == BookingFinanceClearanceStatus.Blocked);
        Assert.Contains(audit.Entries, item => item.Action == AuditActions.BookingFinanceClearanceChanged && item.Reason!.Contains("overdue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lifecycle_DoesNotAllowDraftToDispatchBypass()
    {
        var field = typeof(DispatchTripService).GetField("TransitionMap", BindingFlags.Static | BindingFlags.NonPublic);
        var transitions = Assert.IsAssignableFrom<IReadOnlyDictionary<TripStatus, TripStatus[]>>(field!.GetValue(null));

        Assert.Contains(TripStatus.Planning, transitions[TripStatus.Draft]);
        Assert.DoesNotContain(TripStatus.Dispatched, transitions[TripStatus.Draft]);
        Assert.DoesNotContain(TripStatus.ReadyForDispatch, transitions[TripStatus.Planning]);
    }

    [Fact]
    public void FourTripTypes_HaveDistinctPreDispatchAndCloseRules()
    {
        var expectedDirections = new Dictionary<string, DocumentDirection>
        {
            [DispatchDocumentRules.ExportEmptyPickup] = DocumentDirection.GateOut,
            [DispatchDocumentRules.ExportLadenToTerminal] = DocumentDirection.GateIn,
            [DispatchDocumentRules.ImportLadenDelivery] = DocumentDirection.GateOut,
            [DispatchDocumentRules.EmptyReturn] = DocumentDirection.GateIn
        };

        foreach (var (tripType, direction) in expectedDirections)
        {
            Assert.NotEmpty(DispatchDocumentRules.GetDefaults(tripType, DispatchDocumentMilestone.PreDispatch));
            var close = DispatchDocumentRules.GetDefaults(tripType, DispatchDocumentMilestone.OperationalClose);
            Assert.Contains(close, rule => rule.DocumentCode == "EIR" && rule.Direction == direction);
            Assert.Contains(close, rule => rule.DocumentCode == "DTR");
        }
    }

    [Fact]
    public async Task MultipleDirectionalEirs_RemainActiveAsSeparateRecords()
    {
        await using var db = CreateDbContext();
        var driverId = Guid.NewGuid();
        var trip = new Trip { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), DriverUserId = driverId, Status = TripStatus.AtPickup, CreatedAt = DateTime.UtcNow };
        db.DispatchTrips.Add(trip);
        await db.SaveChangesAsync();
        var service = new DispatchDocumentWorkflowService(db);
        var driver = Actor(driver: true, userId: driverId);

        await service.UploadDocumentAsync(new UploadTripDocumentCommand(trip.Id, TripDocumentType.Eir, "one.jpg", Direction: DocumentDirection.GateOut), driver);
        await service.UploadDocumentAsync(new UploadTripDocumentCommand(trip.Id, TripDocumentType.Eir, "two.jpg", Direction: DocumentDirection.GateIn), driver);

        var eirs = await db.DispatchTripDocuments.Where(item => item.TripId == trip.Id && item.Type == TripDocumentType.Eir).ToListAsync();
        Assert.Equal(2, eirs.Count);
        Assert.All(eirs, item => Assert.True(item.IsActive));
        Assert.Equal(2, eirs.Select(item => item.Direction).Distinct().Count());
    }

    [Fact]
    public async Task SignedDr_CanBePodButOtherDocumentTypesCannot()
    {
        await using var db = CreateDbContext();
        var driverId = Guid.NewGuid();
        var trip = new Trip { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), DriverUserId = driverId, Status = TripStatus.AtDropoff, CreatedAt = DateTime.UtcNow };
        db.DispatchTrips.Add(trip);
        await db.SaveChangesAsync();
        var service = new DispatchDocumentWorkflowService(db);
        var driver = Actor(driver: true, userId: driverId);

        var dr = await service.UploadDocumentAsync(new UploadTripDocumentCommand(trip.Id, TripDocumentType.Dr, "signed-dr.jpg", IsProofOfDelivery: true), driver);
        Assert.True(dr.IsProofOfDelivery);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => service.UploadDocumentAsync(
            new UploadTripDocumentCommand(trip.Id, TripDocumentType.Pod, "pod.jpg", IsProofOfDelivery: true), driver));
    }

    [Fact]
    public async Task AcceptedRecommendation_OnlyMovesCandidateIntoPlanning()
    {
        await using var db = CreateDbContext();
        var actorId = Guid.NewGuid();
        var truckId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        db.Users.Add(new User { Id = actorId, Username = "dispatcher-one", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        db.Assets.Add(new Asset { Id = truckId, AssetCode = "TRK-01", AssetType = AssetType.Truck, Status = AssetStatus.Active, CreatedAt = DateTime.UtcNow });
        db.DispatchTrips.AddRange(
            new Trip { Id = completedId, CustomerId = Guid.NewGuid(), DriverUserId = actorId, TruckAssetId = truckId, Status = TripStatus.DeliveryCompleted, CreatedAt = DateTime.UtcNow.AddHours(-2), Stops = [Stop(completedId, TripStopType.Dropoff, "Davao Port")] },
            new Trip { Id = nextId, CustomerId = Guid.NewGuid(), Status = TripStatus.Draft, CreatedAt = DateTime.UtcNow.AddHours(-1), Stops = [Stop(nextId, TripStopType.Pickup, "Sasa Wharf"), Stop(nextId, TripStopType.Dropoff, "Bunawan Yard")] });
        var recommendation = new DispatchRecommendation { Id = Guid.NewGuid(), CompletedTripId = completedId, RecommendedTripId = nextId, DriverId = actorId, TruckId = truckId, Rank = 1, GeneratedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        db.DispatchRecommendations.Add(recommendation);
        await db.SaveChangesAsync();
        var lifecycle = new RejectingTripLifecycle();
        var controller = new DispatchRecommendationController(db, new EmptySuggestionService(), new NoOpAuditService(), lifecycle)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContext(actorId, RoleNames.Dispatcher) }
        };

        var response = await controller.ConfirmSuggestion(recommendation.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(response.Result);
        var candidate = await db.DispatchTrips.FindAsync(nextId);
        Assert.Equal(TripStatus.Planning, candidate!.Status);
        Assert.Equal(actorId, candidate.DriverUserId);
        Assert.Equal(truckId, candidate.TruckAssetId);
        Assert.Equal(0, lifecycle.DispatchCalls);
        Assert.DoesNotContain(db.DispatchTripStatusHistories, history => history.ToStatus == TripStatus.Dispatched);
    }

    [Fact]
    public async Task Receipt_CalculatesAdditionalChargesDiscountAndTaxWithoutCreatingBillingLifecycle()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Davao Container Services", CreatedAt = DateTime.UtcNow };
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            Status = TripStatus.DeliveryCompleted,
            Rate = 10_000m,
            CreatedAt = DateTime.UtcNow,
            Stops =
            [
                Stop(Guid.Empty, TripStopType.Pickup, "Sasa Port"),
                Stop(Guid.Empty, TripStopType.Dropoff, "Bunawan Yard")
            ]
        };
        foreach (var stop in trip.Stops) stop.TripId = trip.Id;
        db.DispatchTrips.Add(trip);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditService();
        var service = new TripReceiptService(db, audit);

        var result = await service.GenerateAsync(
            trip.Id,
            new GenerateTripReceiptCommand(
                "OR-2026-0142",
                null,
                [new TripReceiptChargeCommand("Toll fee", 500m), new TripReceiptChargeCommand("Waiting time", 750m)],
                1_000m,
                null,
                "Volume discount",
                250m,
                "Bank transfer",
                "BANK-9021",
                "Paid in full"),
            Actor(finance: true));

        Assert.Equal(11_250m, result.Subtotal);
        Assert.Equal(1_000m, result.DiscountAmount);
        Assert.Equal(10_500m, result.Total);
        Assert.Equal("OR-2026-0142", trip.OfficialReceiptNumber);
        Assert.Contains(audit.Entries, item => item.Action == AuditActions.TripReceiptGenerated);

        var history = await service.GetHistoryAsync(trip.Id, Actor(finance: true));
        Assert.Single(history);
        Assert.Equal(2, history.Single().AdditionalCharges.Count);
        Assert.Equal("Davao Container Services", history.Single().CustomerName);

        var reversal = await service.ReverseAsync(trip.Id, result.Id, "Incorrect toll entry.", Actor(finance: true));
        Assert.True(reversal.IsReversal);
        Assert.Equal(result.Id, reversal.ReversesReceiptId);
        Assert.Equal(-10_500m, reversal.Total);
        Assert.Equal(2, (await service.GetHistoryAsync(trip.Id, Actor(finance: true))).Count);

        var corrected = await service.GenerateAsync(trip.Id, new GenerateTripReceiptCommand("OR-2026-0142-C", 10_000m, null, null, null, null, null, null, null, null), Actor(finance: true));
        Assert.Equal(10_000m, corrected.Total);
        Assert.Equal(10_500m, (await service.GetHistoryAsync(trip.Id, Actor(finance: true))).Single(item => item.Id == result.Id).Total);
        Assert.Contains(audit.Entries, item => item.Action == AuditActions.TripReceiptReversed && item.Reason == "Incorrect toll entry.");
    }

    [Fact]
    public async Task Receipt_PercentageDiscountIsRoundedAndCannotBeCombinedWithFixedDiscount()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Receipt Customer", CreatedAt = DateTime.UtcNow };
        var trip = new Trip { Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer, Status = TripStatus.OperationallyClosed, Rate = 999.99m, CreatedAt = DateTime.UtcNow };
        db.DispatchTrips.Add(trip);
        await db.SaveChangesAsync();
        var service = new TripReceiptService(db, new RecordingAuditService());

        var result = await service.GenerateAsync(
            trip.Id,
            new GenerateTripReceiptCommand("OR-2", null, null, null, 10m, null, null, null, null, null),
            Actor(finance: true));
        Assert.Equal(100m, result.DiscountAmount);
        Assert.Equal(899.99m, result.Total);

        await service.ReverseAsync(trip.Id, result.Id, "Prepare validation test.", Actor(finance: true));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => service.GenerateAsync(
            trip.Id,
            new GenerateTripReceiptCommand("OR-3", null, null, 20m, 10m, null, null, null, null, null),
            Actor(finance: true)));
    }

    [Fact]
    public async Task AccountDecision_IsManagerOnlyAndCarriesReasonIntoHistoryAndAudit()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Pending Hauler", AccountStatus = CustomerAccountStatus.PendingReview, AccountRequestedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.DispatchCustomers.Add(customer);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditService();
        var service = new CustomerAccountService(db, audit);

        await Assert.ThrowsAsync<ForbiddenDomainException>(() => service.ApproveAsync(customer.Id, "Reviewed documents.", Actor(dispatcher: true)));
        await service.ApproveAsync(customer.Id, "Manager verified company documents.", Actor(manager: true));

        Assert.Equal(CustomerAccountStatus.ActivePrepaid, customer.AccountStatus);
        Assert.Contains(db.CustomerAccountHistories, item => item.Reason == "Manager verified company documents.");
        Assert.Contains(audit.Entries, item => item.ActorRole == RoleNames.Manager && item.Reason == "Manager verified company documents.");
    }

    [Fact]
    public async Task PendingCustomer_CannotReceivePortalUserUntilAccountIsApproved()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Awaiting approval", AccountStatus = CustomerAccountStatus.PendingReview, CreatedAt = DateTime.UtcNow };
        db.DispatchCustomers.Add(customer);
        await db.SaveChangesAsync();
        var controller = new CustomersController(db, new UserService(db));

        await Assert.ThrowsAsync<ConflictDomainException>(() => controller.CreateCustomerUser(
            customer.Id, new CreateCustomerUserRequest("portal@example.com", "AValidTemporaryPassword1!"), CancellationToken.None));
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task FinanceRecordedOverdueBalance_BlocksNewCreditBookings()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Credit customer", AccountStatus = CustomerAccountStatus.ActiveCredit, CreditStatus = CustomerCreditStatus.Approved, CreditLimit = 100_000m, CreatedAt = DateTime.UtcNow };
        var trip = new Trip { Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer, Status = TripStatus.DeliveryCompleted, CreatedAt = DateTime.UtcNow };
        db.DispatchTrips.Add(trip);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditService();
        var service = new CustomerAccountService(db, audit);

        await service.RecordOverdueTripBalanceAsync(trip.Id, 7_500m, "Customer did not settle the final delivery balance.", Actor(finance: true));

        Assert.True(customer.HasOverdueBalance);
        Assert.Equal(7_500m, customer.OutstandingBalance);
        Assert.Single(db.TripOverdueBalanceRecords);
        Assert.Throws<ConflictDomainException>(() => CustomerAccountService.EnsureCanSubmitBooking(customer));
        Assert.Contains(audit.Entries, item => item.Action == AuditActions.CustomerOverdueBalanceRecorded);
    }

    [Fact]
    public async Task FinanceClearanceEndpoint_AllowsOnlyClearedBookingToConvertToTrip()
    {
        await using var db = CreateDbContext();
        var request = CreateBooking(CustomerAccountStatus.ActivePrepaid, BookingFinanceClearanceStatus.AwaitingPayment);
        request.QuotedAmount = 5_000m;
        db.ShipmentRequests.Add(request);
        await db.SaveChangesAsync();
        var controller = new BookingFinanceController(new BookingFinanceService(db, new RecordingAuditService()), db)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContext(Guid.NewGuid(), RoleNames.HeadOfFinance) }
        };

        var response = await controller.VerifyPayment(request.Id, new VerifyBookingPaymentRequest(5_000m, false, false, "BANK_TRANSFER", "PAY-001", "Full payment verified."), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(BookingFinanceClearanceStatus.Cleared, request.FinanceClearanceStatus);
        var gateway = new RecordingTripGateway();
        await new ShipmentRequestTripCreationService(db, gateway).CreateDraftTripFromApprovedRequestAsync(new CreateTripFromShipmentRequestCommand(request.Id), Actor(dispatcher: true));
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task AccountRequest_IsStoredForManagerReview()
    {
        await using var db = CreateDbContext();
        var service = new CustomerAccountService(db, new RecordingAuditService());

        var customer = await service.SubmitRequestAsync(new CustomerAccountRequestCommand(
            "Demo Fleet",
            "Jamie Santos",
            "jamie@example.com",
            "+63 912 345 6789",
            null,
            "Fleet size: 11-30 trucks\nMessage: Interested in planning."));

        Assert.Equal(CustomerAccountStatus.PendingReview, customer.AccountStatus);
        Assert.Equal(CustomerCreditStatus.NotGranted, customer.CreditStatus);
        Assert.Equal("jamie@example.com", customer.ContactEmail);
        Assert.Equal("Fleet size: 11-30 trucks\nMessage: Interested in planning.", customer.AccountStatusReason);
        Assert.Single(db.DispatchCustomers);
    }

    private static InventoryDbContext CreateDbContext() => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseInMemoryDatabase($"controlled-dispatch-{Guid.NewGuid():N}").Options);

    private static ShipmentRequest CreateBooking(CustomerAccountStatus accountStatus, BookingFinanceClearanceStatus financeStatus)
    {
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Test Customer", AccountStatus = accountStatus, AccountRequestedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        return new ShipmentRequest { Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer, Status = ShipmentRequestStatus.AwaitingFinanceClearance, FinanceClearanceStatus = financeStatus, PickupLocation = "Port", DropoffLocation = "Yard", CreatedAt = DateTime.UtcNow, CreatedByUserId = Guid.NewGuid() };
    }

    private static DispatchActorContext Actor(bool manager = false, bool dispatcher = false, bool driver = false, bool finance = false, Guid? userId = null) =>
        new(userId ?? Guid.NewGuid(), manager, dispatcher, driver, finance, false);

    private static TripStop Stop(Guid tripId, TripStopType type, string location) =>
        new() { Id = Guid.NewGuid(), TripId = tripId, StopType = type, LocationText = location, CreatedAt = DateTime.UtcNow };

    private static DefaultHttpContext HttpContext(Guid userId, string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "test"));
        return context;
    }

    private sealed class RecordingTripGateway : IShipmentRequestTripDispatchGateway
    {
        public int Calls { get; private set; }
        public Task<Guid> CreateDraftTripAsync(ShipmentRequestTripDraftData tripDraft, DispatchActorContext actor, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(Guid.NewGuid()); }
    }

    private sealed class EmptySuggestionService : ITripChainingSuggestionService
    {
        public Task<List<DispatchRecommendation>> GenerateSuggestionsAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(new List<DispatchRecommendation>());
    }

    private sealed class RejectingTripLifecycle : ITripLifecycleService
    {
        public int DispatchCalls { get; private set; }
        public Task<Trip> CreateDraftAsync(CreateDispatchTripCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Trip> UpdateTripAsync(Guid tripId, UpdateDispatchTripCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Trip> DispatchAsync(DispatchTripCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default) { DispatchCalls++; throw new InvalidOperationException("Dispatch release must not be called."); }
        public Task<Trip> ChangeStatusAsync(ChangeDispatchTripStatusCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Trip> CorrectStatusAsync(CorrectDispatchTripStatusCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<(string Action, string? ActorRole, string? Reason)> Entries { get; } = [];
        public void AddEntry(Guid actorUserId, string action, string entityType, Guid entityId, object? before = null, object? after = null, string? actorRole = null, Guid? tripId = null, string? reason = null, Guid? relatedAttachmentId = null, string? referenceNumber = null) => Entries.Add((action, actorRole, reason));
    }
}
