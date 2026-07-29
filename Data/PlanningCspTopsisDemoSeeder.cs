using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Data;

/// <summary>
/// An isolated, repeatable fixture for inspecting planning CSP and TOPSIS behavior.
/// The batch is intentionally identified entirely by its own names/references so that
/// cleanup never touches ordinary development data.
/// </summary>
public sealed class PlanningCspTopsisDemoSeeder
{
    public const string BatchKey = "PLANNING-CSP-TOPSIS-DEMO-V1";
    private const string CustomerName = "CSP Demo Planning Customer";
    public const string DriverDemoPassword = "CspDemoDriver1!";
    private readonly InventoryDbContext _db;
    private readonly IHostEnvironment _environment;
    private readonly IPasswordHashService _passwordHashService;

    public PlanningCspTopsisDemoSeeder(InventoryDbContext db, IHostEnvironment environment, IPasswordHashService passwordHashService)
    {
        _db = db;
        _environment = environment;
        _passwordHashService = passwordHashService;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        EnsureAllowed();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await ClearCoreAsync(cancellationToken);

        var now = DateTime.UtcNow;
        // 10:00 Davao time on a date safely in the future (UTC+08:00).
        var basePickup = now.Date.AddDays(7).AddHours(2);
        var customer = new Customer { Id = Guid.NewGuid(), Name = CustomerName, CreatedAt = now };
        _db.DispatchCustomers.Add(customer);
        var driverRoleId = await _db.Roles.Where(role => role.Name == RoleNames.Driver).Select(role => (int?)role.Id).SingleOrDefaultAsync(cancellationToken);
        if (!driverRoleId.HasValue)
        {
            throw new InvalidOperationException("The Driver role is required before seeding the planning demo.");
        }

        var drivers = new Dictionary<string, User>();
        foreach (var spec in new[]
        {
            ("juan", "CSP Demo - Juan BestFit", true, "Active"),
            ("allan", "CSP Demo - Allan Alternative", true, "Active"),
            ("rene", "CSP Demo - Rene Acceptable", true, "Active"),
            ("marco", "CSP Demo - Marco Overlap", true, "Active"),
            ("nilo", "CSP Demo - Nilo Unreachable", true, "Active"),
            ("benjie", "CSP Demo - Benjie Unavailable", false, "Unavailable")
        })
        {
            var user = new User
            {
                Id = Guid.NewGuid(), Username = spec.Item2, Email = $"csp-demo-{spec.Item1}@example.invalid",
                PasswordHash = _passwordHashService.HashPassword(DriverDemoPassword), IsActive = spec.Item3, CreatedAt = now
            };
            drivers.Add(spec.Item1, user);
            _db.Users.Add(user);
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = driverRoleId.Value });
            _db.DispatchDrivers.Add(new Driver
            {
                Id = Guid.NewGuid(), UserId = user.Id, LicenseNumber = $"CSP-DEMO-DL-{spec.Item1.ToUpperInvariant()}",
                Status = spec.Item4, CreatedAt = now
            });
        }

        var trucks = new Dictionary<string, Asset>();
        foreach (var spec in new[]
        {
            ("1001", "40ft capable", 7.0700m, 125.6000m, "Active"),
            ("1002", "40ft capable", 7.1600m, 125.6600m, "Active"),
            ("1003", "40ft capable", 7.2300m, 125.7100m, "Active"),
            ("1004", "40ft capable", 7.4000m, 125.7000m, "Active"),
            ("1005", "40ft capable", 7.1000m, 125.6200m, "Active"),
            ("1006", "20ft capable", 7.1000m, 125.6200m, "Active"),
            ("1007", "40ft capable", 2.0000m, 125.6200m, "Active"),
            // Kept outside the Planning candidate pool by the reachability rule, but available to
            // the post-delivery CSP/TOPSIS demo after Juan confirms delivery.
            ("1008", "40ft capable", 2.1000m, 125.6200m, "Active")
        })
        {
            var asset = new Asset { Id = Guid.NewGuid(), AssetType = AssetType.Truck, AssetCode = $"CSP-TRK-{spec.Item1}", PlateNo = $"CSP-TRK-{spec.Item1}", Status = AssetStatus.Active, CreatedAt = now };
            trucks.Add(spec.Item1, asset);
            _db.Assets.Add(asset);
            _db.DispatchTrucks.Add(new Truck
            {
                Id = Guid.NewGuid(), AssetId = asset.Id, PlateNumber = asset.AssetCode, ContainerCapability = spec.Item2,
                Status = spec.Item5, LastLatitude = spec.Item3, LastLongitude = spec.Item4, LastLocationAt = now.AddMinutes(-5), CreatedAt = now
            });
        }

        // The maintenance row exercises the real maintenance query used by CSP.
        _db.Requests.Add(new Request
        {
            Id = Guid.NewGuid(), RequestType = RequestType.MaintenanceIssue, RequesterUserId = drivers["juan"].Id,
            AssetId = trucks["1005"].Id, Purpose = BatchKey, Status = RequestStatus.Submitted, SubmittedAt = now, CreatedAt = now
        });

        var scenarios = new Dictionary<string, Trip>();
        scenarios["CSP-DEMO-001-GOLD-PATH"] = AddScenario(customer, drivers["juan"].Id, now, basePickup, "CSP-DEMO-001-GOLD-PATH", true, "CSPGOLD001", includeAtw: true);
        scenarios["CSP-DEMO-002-EXCLUSIONS"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(1), "CSP-DEMO-002-EXCLUSIONS", true, "CSPEX002", includeAtw: true);
        scenarios["CSP-DEMO-003-ATW-HANDOFF-BLOCKER"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(2), "CSP-DEMO-003-ATW-HANDOFF-BLOCKER", true, "CSPATW003", includeAtw: false);
        scenarios["CSP-DEMO-004-CONTAINER-HANDOFF-BLOCKER"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(3), "CSP-DEMO-004-CONTAINER-HANDOFF-BLOCKER", true, null, includeAtw: true);
        scenarios["CSP-DEMO-005-PAST-SCHEDULE"] = AddScenario(customer, drivers["juan"].Id, now, now.AddHours(-3), "CSP-DEMO-005-PAST-SCHEDULE", true, "CSPPAST005", includeAtw: true);
        scenarios["CSP-DEMO-006-NO-FEASIBLE-ASSIGNMENT"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(4), "CSP-DEMO-006-NO-FEASIBLE-ASSIGNMENT", true, "CSPNONE006", includeAtw: true);
        scenarios["CSP-DEMO-007-STALE-RECOMMENDATION"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(5), "CSP-DEMO-007-STALE-RECOMMENDATION", true, "CSPSTALE007", includeAtw: true);
        scenarios["CSP-DEMO-008-MANUAL-OVERRIDE"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(6), "CSP-DEMO-008-MANUAL-OVERRIDE", true, "CSPOVR008", includeAtw: true);
        scenarios["CSP-DEMO-009-CONCURRENCY-A"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(7), "CSP-DEMO-009-CONCURRENCY-A", true, "CSPCON009A", includeAtw: true);
        scenarios["CSP-DEMO-009-CONCURRENCY-B"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(7), "CSP-DEMO-009-CONCURRENCY-B", true, "CSPCON009B", includeAtw: true);
        scenarios["CSP-DEMO-010-TOPSIS-WEIGHTS"] = AddScenario(customer, drivers["juan"].Id, now, basePickup.AddDays(8), "CSP-DEMO-010-TOPSIS-WEIGHTS", true, "CSPTOP010", includeAtw: true);
        var readyToDeliver = AddReadyToDeliverTrip(customer, drivers["juan"].Id, trucks["1008"].Id, now);

        // Scenario 1: keep one isolated feasible combination (Juan + 1001).
        // The rest of the fleet is made unavailable in this exact window through real overlapping trips,
        // maintenance, capability, or reachability constraints.
        AddSupportTrip(customer, drivers["allan"].Id, trucks["1002"].Id, now, basePickup, "CSP-DEMO-SUPPORT-GOLD-ALLAN");
        AddSupportTrip(customer, drivers["rene"].Id, trucks["1003"].Id, now, basePickup, "CSP-DEMO-SUPPORT-GOLD-RENE");
        AddSupportTrip(customer, drivers["marco"].Id, trucks["1004"].Id, now, basePickup, "CSP-DEMO-SUPPORT-GOLD-MARCO");
        AddSupportTrip(customer, drivers["nilo"].Id, trucks["1005"].Id, now, basePickup, "CSP-DEMO-SUPPORT-GOLD-NILO");

        // Scenario 2: one driver and three compatible trucks produce a deterministic TOPSIS order.
        // Marco/1004 supplies the explicit overlap rejection; maintenance, mismatch and reachability
        // exclude the remaining trucks. Allan, Rene and Nilo are unavailable only for this window.
        AddSupportTrip(customer, drivers["marco"].Id, trucks["1004"].Id, now, basePickup.AddDays(1), "CSP-DEMO-SUPPORT-EXCLUSION-OVERLAP");
        AddSupportTrip(customer, drivers["allan"].Id, trucks["1004"].Id, now, basePickup.AddDays(1), "CSP-DEMO-SUPPORT-RANKING-ALLAN");
        AddSupportTrip(customer, drivers["rene"].Id, trucks["1004"].Id, now, basePickup.AddDays(1), "CSP-DEMO-SUPPORT-RANKING-RENE");
        AddSupportTrip(customer, drivers["nilo"].Id, trucks["1004"].Id, now, basePickup.AddDays(1), "CSP-DEMO-SUPPORT-RANKING-NILO");

        // Scenario 6: each normally valid driver/truck is occupied in this exact window.
        var infeasible = basePickup.AddDays(4);
        AddSupportTrip(customer, drivers["benjie"].Id, trucks["1001"].Id, now, infeasible, "CSP-DEMO-SUPPORT-NO-FEASIBLE-1");
        AddSupportTrip(customer, drivers["allan"].Id, trucks["1002"].Id, now, infeasible, "CSP-DEMO-SUPPORT-NO-FEASIBLE-2");
        AddSupportTrip(customer, drivers["rene"].Id, trucks["1003"].Id, now, infeasible, "CSP-DEMO-SUPPORT-NO-FEASIBLE-3");
        AddSupportTrip(customer, drivers["marco"].Id, trucks["1004"].Id, now, infeasible, "CSP-DEMO-SUPPORT-NO-FEASIBLE-4");
        AddSupportTrip(customer, drivers["nilo"].Id, trucks["1005"].Id, now, infeasible, "CSP-DEMO-SUPPORT-NO-FEASIBLE-5");

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        PrintSummary(scenarios, readyToDeliver);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        EnsureAllowed();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await ClearCoreAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Console.WriteLine("Planning CSP-TOPSIS demo seed removed.");
    }

    private Trip AddScenario(Customer customer, Guid actorId, DateTime now, DateTime pickupAt, string reference, bool approved, string? containerNumber, bool includeAtw)
    {
        var bookingReference = ToBookingReference(reference);
        var request = new ShipmentRequest
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Status = approved ? ShipmentRequestStatus.ConvertedToTrip : ShipmentRequestStatus.Approved,
            PickupLocation = "Sasa Wharf, Davao City", PickupLatitude = 7.1100m, PickupLongitude = 125.6200m,
            DropoffLocation = "Davao Container Yard", DropoffLatitude = 7.0800m, DropoffLongitude = 125.6100m,
            RequestedPickupTime = pickupAt, ContainerSize = ContainerSize.FortyFt.ToString(), TripType = TripType.PortPickup.ToString(),
            ContainerNumber = containerNumber, BookingNumber = bookingReference, ShippingLine = "CSP Demo Shipping", CreatedAt = now, CreatedByUserId = actorId,
            ApprovedAt = now, ApprovedByUserId = actorId
        };
        var trip = new Trip
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, Status = TripStatus.Draft, ContainerNumber = containerNumber,
            ContainerSize = ContainerSize.FortyFt.ToString(), TripType = TripType.PortPickup.ToString(), BookingNumber = bookingReference,
            ShippingLine = "CSP Demo Shipping", Notes = $"{BatchKey}: {reference}", CreatedAt = now
        };
        request.ConvertedTripId = trip.Id;
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Pickup, LocationText = request.PickupLocation, Latitude = request.PickupLatitude, Longitude = request.PickupLongitude, ScheduledAt = pickupAt, CreatedAt = now });
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Dropoff, LocationText = request.DropoffLocation, Latitude = request.DropoffLatitude, Longitude = request.DropoffLongitude, ScheduledAt = pickupAt.AddHours(4), CreatedAt = now });
        if (includeAtw)
        {
            trip.Documents.Add(new TripDocument { Id = Guid.NewGuid(), TripId = trip.Id, Type = TripDocumentType.Atw, State = TripDocumentState.Verified, StorageKey = $"{BatchKey}/{reference}/atw.pdf", UploadedByUserId = actorId, VerifiedByUserId = actorId, UploadedAt = now, VerifiedAt = now, IsActive = true });
        }
        _db.ShipmentRequests.Add(request);
        _db.DispatchTrips.Add(trip);
        return trip;
    }

    private static string ToBookingReference(string reference) => reference switch
    {
        "CSP-DEMO-003-ATW-HANDOFF-BLOCKER" => "CSP-DEMO-003-ATW-BLOCKER",
        "CSP-DEMO-004-CONTAINER-HANDOFF-BLOCKER" => "CSP-DEMO-004-CNTR-BLOCKER",
        "CSP-DEMO-006-NO-FEASIBLE-ASSIGNMENT" => "CSP-DEMO-006-NO-FEASIBLE",
        "CSP-DEMO-007-STALE-RECOMMENDATION" => "CSP-DEMO-007-STALE-REC",
        _ when reference.Length > 30 => reference[..30],
        _ => reference
    };

    private void AddSupportTrip(Customer customer, Guid driverId, Guid truckId, DateTime now, DateTime pickupAt, string reference)
    {
        var trip = new Trip { Id = Guid.NewGuid(), CustomerId = customer.Id, DriverUserId = driverId, TruckAssetId = truckId, Status = TripStatus.ReadyForDispatch, ContainerNumber = "CSPSUPPORT", ContainerSize = ContainerSize.FortyFt.ToString(), BookingNumber = ToBookingReference(reference), Notes = BatchKey, CreatedAt = now };
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Pickup, LocationText = "CSP Demo Support Pickup", Latitude = 7.1100m, Longitude = 125.6200m, ScheduledAt = pickupAt, CreatedAt = now });
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Dropoff, LocationText = "CSP Demo Support Dropoff", Latitude = 7.0800m, Longitude = 125.6100m, ScheduledAt = pickupAt.AddHours(4), CreatedAt = now });
        _db.DispatchTrips.Add(trip);
    }

    private Trip AddReadyToDeliverTrip(Customer customer, Guid driverId, Guid truckId, DateTime now)
    {
        const string reference = "CSP-DEMO-011-FINISH-DELIVERY";
        var trip = new Trip
        {
            Id = Guid.NewGuid(), CustomerId = customer.Id, DriverUserId = driverId, TruckAssetId = truckId,
            Status = TripStatus.AtDropoff, ContainerNumber = "CSPDEL011", ContainerSize = ContainerSize.FortyFt.ToString(),
            TripType = TripType.PortPickup.ToString(), BookingNumber = reference, ShippingLine = "CSP Demo Shipping",
            Notes = $"{BatchKey}: ready for a Driver to confirm delivery.", CreatedAt = now.AddHours(-4), UpdatedAt = now
        };
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Pickup, LocationText = "Sasa Wharf, Davao City", Latitude = 7.1100m, Longitude = 125.6200m, ScheduledAt = now.AddHours(-4), ActualAt = now.AddHours(-4), CreatedAt = now.AddHours(-4) });
        trip.Stops.Add(new TripStop { Id = Guid.NewGuid(), TripId = trip.Id, StopType = TripStopType.Dropoff, LocationText = "Davao Container Yard", Latitude = 7.0800m, Longitude = 125.6100m, ScheduledAt = now.AddHours(-1), ActualAt = now.AddMinutes(-10), CreatedAt = now.AddHours(-4) });
        foreach (var documentType in new[] { TripDocumentType.Atw, TripDocumentType.Eir, TripDocumentType.GatePass, TripDocumentType.Dr, TripDocumentType.Pod })
        {
            trip.Documents.Add(new TripDocument { Id = Guid.NewGuid(), TripId = trip.Id, Type = documentType, State = documentType == TripDocumentType.Atw ? TripDocumentState.Verified : TripDocumentState.Uploaded, StorageKey = $"{BatchKey}/{reference}/{documentType}.jpg", UploadedByUserId = driverId, VerifiedByUserId = documentType == TripDocumentType.Atw ? driverId : null, UploadedAt = now.AddMinutes(-30), VerifiedAt = documentType == TripDocumentType.Atw ? now.AddMinutes(-25) : null, IsActive = true });
        }
        _db.DispatchTrips.Add(trip);
        return trip;
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        var tripIds = await _db.DispatchTrips.Where(t => t.Notes == BatchKey || (t.BookingNumber != null && t.BookingNumber.StartsWith("CSP-DEMO-"))).Select(t => t.Id).ToListAsync(cancellationToken);
        var requestIds = await _db.ShipmentRequests.Where(r => r.BookingNumber != null && r.BookingNumber.StartsWith("CSP-DEMO-")).Select(r => r.Id).ToListAsync(cancellationToken);
        var userIds = await _db.Users.Where(u => u.Username.StartsWith("CSP Demo -")).Select(u => u.Id).ToListAsync(cancellationToken);
        var assetIds = await _db.Assets.Where(a => a.AssetCode.StartsWith("CSP-TRK-")).Select(a => a.Id).ToListAsync(cancellationToken);

        _db.DispatchTripDocuments.RemoveRange(_db.DispatchTripDocuments.Where(d => tripIds.Contains(d.TripId)));
        _db.GeneratedWaybills.RemoveRange(_db.GeneratedWaybills.Where(d => tripIds.Contains(d.TripId)));
        _db.DispatchTripLocationPings.RemoveRange(_db.DispatchTripLocationPings.Where(d => tripIds.Contains(d.TripId)));
        _db.DispatchTripStatusHistories.RemoveRange(_db.DispatchTripStatusHistories.Where(d => tripIds.Contains(d.TripId)));
        _db.DispatchTripStops.RemoveRange(_db.DispatchTripStops.Where(d => tripIds.Contains(d.TripId)));
        _db.DispatchTrips.RemoveRange(_db.DispatchTrips.Where(d => tripIds.Contains(d.Id)));
        _db.ShipmentRequestDocuments.RemoveRange(_db.ShipmentRequestDocuments.Where(d => requestIds.Contains(d.RequestId)));
        _db.ShipmentRequests.RemoveRange(_db.ShipmentRequests.Where(d => requestIds.Contains(d.Id)));
        _db.Requests.RemoveRange(_db.Requests.Where(r => r.Purpose == BatchKey));
        _db.DispatchDrivers.RemoveRange(_db.DispatchDrivers.Where(d => userIds.Contains(d.UserId)));
        _db.DispatchTrucks.RemoveRange(_db.DispatchTrucks.Where(t => assetIds.Contains(t.AssetId)));
        _db.DispatchTrailers.RemoveRange(_db.DispatchTrailers.Where(t => assetIds.Contains(t.AssetId)));
        _db.Assets.RemoveRange(_db.Assets.Where(a => assetIds.Contains(a.Id)));
        _db.AuthEvents.RemoveRange(_db.AuthEvents.Where(e => e.UserId.HasValue && userIds.Contains(e.UserId.Value)));
        _db.RefreshTokens.RemoveRange(_db.RefreshTokens.Where(token => userIds.Contains(token.UserId)));
        _db.MfaChallenges.RemoveRange(_db.MfaChallenges.Where(challenge => userIds.Contains(challenge.UserId)));
        _db.PushNotificationTokens.RemoveRange(_db.PushNotificationTokens.Where(token => userIds.Contains(token.UserId)));
        _db.AuditLogs.RemoveRange(_db.AuditLogs.Where(log => userIds.Contains(log.ActorUserId)));
        _db.UserRoles.RemoveRange(_db.UserRoles.Where(ur => userIds.Contains(ur.UserId)));
        _db.Users.RemoveRange(_db.Users.Where(u => userIds.Contains(u.Id)));
        _db.DispatchCustomers.RemoveRange(_db.DispatchCustomers.Where(c => c.Name == CustomerName));
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    private void EnsureAllowed()
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
        {
            throw new InvalidOperationException("Planning CSP-TOPSIS demo seeding is allowed only in Development or Test.");
        }
    }

    private static void PrintSummary(IReadOnlyDictionary<string, Trip> scenarios, Trip readyToDeliver)
    {
        Console.WriteLine("Planning CSP-TOPSIS demo seed complete");
        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"{scenario.Key}: trip {scenario.Value.Id}");
        }
        Console.WriteLine("CSP-DEMO-001: one valid Gold-path assignment — CSP Demo - Juan BestFit + CSP-TRK-1001.");
        Console.WriteLine("CSP-DEMO-002: three ranked options — CSP-TRK-1001, CSP-TRK-1002, CSP-TRK-1003; exclusions exercise DRIVER_OVERLAP, TRUCK_OVERLAP, TRUCK_MAINTENANCE, EQUIPMENT_INCOMPATIBLE, PICKUP_UNREACHABLE.");
        Console.WriteLine("CSP-DEMO-003/004: recommendations expected; Ready handoff blocked until ATW/container is resolved.");
        Console.WriteLine("CSP-DEMO-005: booking blocker; resources are not evaluated. CSP-DEMO-006: evaluated with zero feasible combinations.");
        Console.WriteLine($"CSP-DEMO-011-FINISH-DELIVERY: At Dropoff and ready to confirm delivery — trip {readyToDeliver.Id}.");
        Console.WriteLine("Driver login: CSP Demo - Juan BestFit / CspDemoDriver1!");
    }
}
