using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public sealed class DemoDataSeeder
{
    public const string SuperAdminUsername = "Superadmin";
    public const string SuperAdminEmail = "Superadmin@nvg.com";
    public const string SuperAdminPassword = "SuperAdminDemo1!";

    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;
    private readonly AssetService _assetService;
    private readonly InventoryService _inventoryService;
    private readonly RequestService _requestService;
    private readonly RequestWorkflowService _requestWorkflow;
    private readonly LoanWorkflowService _loanWorkflow;
    private readonly PurchaseOrderWorkflowService _purchaseOrderWorkflow;
    private readonly InventoryAdjustmentWorkflowService _adjustmentWorkflow;
    private readonly SupplierService _supplierService;
    private readonly IHostEnvironment _environment;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        InventoryDbContext dbContext,
        UserService userService,
        AssetService assetService,
        InventoryService inventoryService,
        RequestService requestService,
        RequestWorkflowService requestWorkflow,
        LoanWorkflowService loanWorkflow,
        PurchaseOrderWorkflowService purchaseOrderWorkflow,
        InventoryAdjustmentWorkflowService adjustmentWorkflow,
        SupplierService supplierService,
        IHostEnvironment environment,
        IPasswordHashService passwordHashService,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _userService = userService;
        _assetService = assetService;
        _inventoryService = inventoryService;
        _requestService = requestService;
        _requestWorkflow = requestWorkflow;
        _loanWorkflow = loanWorkflow;
        _purchaseOrderWorkflow = purchaseOrderWorkflow;
        _adjustmentWorkflow = adjustmentWorkflow;
        _supplierService = supplierService;
        _environment = environment;
        _passwordHashService = passwordHashService;
        _logger = logger;
    }

    public async Task SeedAsync(bool resetDatabase = true, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("Demo seeding allowed only in Development.");
        }

        if (resetDatabase)
        {
            DatabaseResetGuard.EnsureSafeToReset(_environment, _dbContext);
            await _dbContext.Database.MigrateAsync(cancellationToken);
            await WipeDevSeedDataAsync(cancellationToken);
        }

        await EnsureSuperAdminAsync(cancellationToken);
        await SeedDispatchDemoAsync(cancellationToken);
        await SeedInventoryDemoAsync(cancellationToken);
        await MfaSeedHelper.EnsureAdminMfaEnabledAsync(_dbContext, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await LogSeedSummaryAsync(cancellationToken);
    }

    private async Task WipeDevSeedDataAsync(CancellationToken cancellationToken)
    {
        DatabaseResetGuard.EnsureSafeToReset(_environment, _dbContext);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var deleteSql = """
                DELETE FROM dbo.audit_logs;
                DELETE FROM dbo.auth_events;
                DELETE FROM dbo.dispatch_recommendations;
                DELETE FROM dbo.dispatch_trip_status_history;
                DELETE FROM dbo.dispatch_trip_documents;
                DELETE FROM dbo.dispatch_trip_stops;
                DELETE FROM dbo.shipment_request_documents;
                UPDATE dbo.shipment_requests SET converted_trip_id = NULL;
                DELETE FROM dbo.dispatch_trips;
                DELETE FROM dbo.shipment_requests;
                UPDATE dbo.users SET customer_id = NULL;
                DELETE FROM dbo.dispatch_customers;
                DELETE FROM dbo.dispatch_drivers;
                DELETE FROM dbo.dispatch_trucks;
                DELETE FROM dbo.dispatch_trailers;
                DELETE FROM dbo.mfa_challenges;
                DELETE FROM dbo.refresh_tokens;
                DELETE FROM dbo.user_roles;
                DELETE FROM dbo.approval_actions;
                DELETE FROM dbo.approvals;
                DELETE FROM dbo.loan_line_returns;
                DELETE FROM dbo.loan_lines;
                DELETE FROM dbo.loans;
                DELETE FROM dbo.request_lines;
                DELETE FROM dbo.requests;
                DELETE FROM dbo.inventory_adjustment_lines;
                DELETE FROM dbo.inventory_adjustments;
                IF OBJECT_ID(N'dbo.purchase_order_receipt_lines', N'U') IS NOT NULL
                    DELETE FROM dbo.purchase_order_receipt_lines;
                DELETE FROM dbo.purchase_order_receipts;
                DELETE FROM dbo.purchase_order_lines;
                DELETE FROM dbo.purchase_orders;
                DELETE FROM dbo.stock_logs;
                DELETE FROM dbo.kit_components;
                DELETE FROM dbo.inventory;
                DELETE FROM dbo.suppliers;
                DELETE FROM dbo.assets;
                DELETE FROM dbo.users;
                """;

            await _dbContext.Database.ExecuteSqlRawAsync(deleteSql, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedDispatchDemoAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.DispatchTrips.AnyAsync(cancellationToken))
        {
            return;
        }

        var roleIds = await _dbContext.Roles.ToDictionaryAsync(role => role.Name, role => role.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var passwordHash = _passwordHashService.HashPassword(SuperAdminPassword);

        var users = new Dictionary<string, User>
        {
            ["dispatcher"] = NewUser("dispatcher.davao", "dispatch@nvg.local", passwordHash, now),
            ["manager"] = NewUser("ops.manager", "manager@nvg.local", passwordHash, now),
            ["finance"] = NewUser("finance.head", "finance@nvg.local", passwordHash, now),
            ["ceo"] = NewUser("ceo", "ceo@nvg.local", passwordHash, now),
            ["inventory"] = NewUser("inventory.officer", "inventory@nvg.local", passwordHash, now),
            ["driverA"] = NewUser("juan.delacruz", "juan.delacruz@nvg.local", passwordHash, now),
            ["driverB"] = NewUser("marco.santos", "marco.santos@nvg.local", passwordHash, now),
            ["driverC"] = NewUser("rene.garcia", "rene.garcia@nvg.local", passwordHash, now),
            ["driverD"] = NewUser("allan.tan", "allan.tan@nvg.local", passwordHash, now),
            ["driverE"] = NewUser("benjie.ramos", "benjie.ramos@nvg.local", passwordHash, now),
            ["driverF"] = NewUser("nilo.bautista", "nilo.bautista@nvg.local", passwordHash, now)
        };

        _dbContext.Users.AddRange(users.Values);
        _dbContext.UserRoles.AddRange(
            NewUserRole(users["dispatcher"], roleIds[RoleNames.Dispatcher]),
            NewUserRole(users["manager"], roleIds[RoleNames.Manager]),
            NewUserRole(users["finance"], roleIds[RoleNames.HeadOfFinance]),
            NewUserRole(users["ceo"], roleIds[RoleNames.Ceo]),
            NewUserRole(users["inventory"], roleIds[RoleNames.InventoryOfficer]),
            NewUserRole(users["driverA"], roleIds[RoleNames.Driver]),
            NewUserRole(users["driverB"], roleIds[RoleNames.Driver]),
            NewUserRole(users["driverC"], roleIds[RoleNames.Driver]),
            NewUserRole(users["driverD"], roleIds[RoleNames.Driver]),
            NewUserRole(users["driverE"], roleIds[RoleNames.Driver]),
            NewUserRole(users["driverF"], roleIds[RoleNames.Driver]));

        var customers = new[]
        {
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "KUDOS Logistics",
                Address = "Tagum City, Davao del Norte",
                ContactPerson = "Mara Villanueva",
                ContactEmail = "dispatch@kudos-logistics.example",
                Phone = "+63 84 555 0181",
                Contact = "Tagum dispatch desk",
                CreatedAt = now.AddDays(-45)
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                Name = "KTC Operations",
                Address = "Davao City",
                ContactPerson = "Rogelio Uy",
                ContactEmail = "ops@ktc-operations.example",
                Phone = "+63 82 555 0182",
                Contact = "Davao operations desk",
                CreatedAt = now.AddDays(-38)
            }
        };
        _dbContext.DispatchCustomers.AddRange(customers);

        var customerUsers = new[]
        {
            NewUser("client.kudos", "client.kudos@nvg.local", passwordHash, now),
            NewUser("client.ktc", "client.ktc@nvg.local", passwordHash, now)
        };
        customerUsers[0].CustomerId = customers[0].Id;
        customerUsers[1].CustomerId = customers[1].Id;
        _dbContext.Users.AddRange(customerUsers);
        _dbContext.UserRoles.AddRange(
            NewUserRole(customerUsers[0], roleIds[RoleNames.Customer]),
            NewUserRole(customerUsers[1], roleIds[RoleNames.Customer]));

        var trucks = new[]
        {
            NewAsset("NVG-1001", AssetType.Truck, "NVG-1001", now),
            NewAsset("NVG-1002", AssetType.Truck, "NVG-1002", now),
            NewAsset("NVG-1003", AssetType.Truck, "NVG-1003", now),
            NewAsset("NVG-1004", AssetType.Truck, "NVG-1004", now),
            NewAsset("NVG-1005", AssetType.Truck, "NVG-1005", now),
            NewAsset("NVG-1006", AssetType.Truck, "NVG-1006", now)
        };
        var trailers = new[]
        {
            NewAsset("TRL-001", AssetType.Trailer, "TRL-001", now),
            NewAsset("TRL-002", AssetType.Trailer, "TRL-002", now),
            NewAsset("TRL-003", AssetType.Trailer, "TRL-003", now),
            NewAsset("TRL-004", AssetType.Trailer, "TRL-004", now)
        };
        _dbContext.Assets.AddRange(trucks);
        _dbContext.Assets.AddRange(trailers);
        _dbContext.DispatchDrivers.AddRange(
            NewDriver(users["driverA"], "DL-2025-001", now),
            NewDriver(users["driverB"], "DL-2025-002", now),
            NewDriver(users["driverC"], "DL-2025-003", now),
            NewDriver(users["driverD"], "DL-2025-004", now),
            NewDriver(users["driverE"], "DL-2025-005", now),
            NewDriver(users["driverF"], "DL-2025-006", now));
        _dbContext.DispatchTrucks.AddRange(
            NewTruck(trucks[0], "20ft capable", now),
            NewTruck(trucks[1], "40ft capable", now),
            NewTruck(trucks[2], "20ft capable", now),
            NewTruck(trucks[3], "40ft capable", now),
            NewTruck(trucks[4], "20ft/40ft capable", now),
            NewTruck(trucks[5], "40ft capable", now));
        _dbContext.DispatchTrailers.AddRange(
            NewTrailer(trailers[0], "20ft", now),
            NewTrailer(trailers[1], "40ft", now),
            NewTrailer(trailers[2], "20ft", now),
            NewTrailer(trailers[3], "40ft", now));

        var dispatcher = users["dispatcher"];
        var manager = users["manager"];
        var trips = new List<Trip>();
        var driverPool = new[]
        {
            users["driverA"],
            users["driverB"],
            users["driverC"],
            users["driverD"],
            users["driverE"],
            users["driverF"]
        };
        var specs = GenerateTripSpecs(customers, driverPool, trucks, now);

        foreach (var spec in specs)
        {
            var trip = NewTrip(spec, dispatcher, manager, now);
            trips.Add(trip);
        }

        _dbContext.DispatchTrips.AddRange(trips);
        _dbContext.DispatchTripStops.AddRange(trips.SelectMany(trip => trip.Stops));
        _dbContext.DispatchTripStatusHistories.AddRange(trips.SelectMany(trip => trip.StatusHistory));
        _dbContext.DispatchTripDocuments.AddRange(NewTripDocuments(trips, dispatcher, manager));
        _dbContext.GeneratedWaybills.AddRange(NewGeneratedWaybills(trips, dispatcher));
        _dbContext.DispatchRecommendations.AddRange(NewDispatchRecommendations(trips, dispatcher, now));

        _dbContext.ShipmentRequests.AddRange(GenerateShipmentRequests(customers, customerUsers, trips, manager.Id, now));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryDemoAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.InventoryItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var actor = await _dbContext.Users.FirstAsync(user => user.Username == "inventory.officer", cancellationToken);
        var borrowerA = await _dbContext.Users.FirstAsync(user => user.Username == "juan.delacruz", cancellationToken);
        var borrowerB = await _dbContext.Users.FirstAsync(user => user.Username == "marco.santos", cancellationToken);
        var borrowerC = await _dbContext.Users.FirstAsync(user => user.Username == "rene.garcia", cancellationToken);
        var borrowerD = await _dbContext.Users.FirstAsync(user => user.Username == "allan.tan", cancellationToken);
        var now = DateTime.UtcNow;
        var items = new[]
        {
            NewInventoryItem("Engine Oil (SAE 40)", "L", ItemType.Consumable, 45m, 10m, 285m, "Davao yard stores", now),
            NewInventoryItem("Brake Pads (Heavy Duty)", "set", ItemType.Consumable, 8m, 10m, 1850m, "Panabo maintenance shelf", now),
            NewInventoryItem("Tire (10.00R20)", "pcs", ItemType.Consumable, 12m, 5m, 14500m, "Davao tire rack", now),
            NewInventoryItem("Fuel Filter", "pcs", ItemType.Consumable, 3m, 5m, 650m, "Davao yard bin B1", now),
            NewInventoryItem("Air Filter", "pcs", ItemType.Consumable, 20m, 8m, 780m, "Davao yard bin B2", now),
            NewInventoryItem("Hydraulic Oil", "L", ItemType.Consumable, 15m, 6m, 240m, "Panabo maintenance shelf", now),
            NewInventoryItem("Grease (Lithium)", "kg", ItemType.Consumable, 30m, 10m, 175m, "Davao lube cabinet", now),
            NewInventoryItem("Coolant", "L", ItemType.Consumable, 6m, 8m, 190m, "Davao lube cabinet", now)
        };
        var itemMap = items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var suppliers = new[]
        {
            NewSupplier("NVG Parts Supply", "Davao City", now),
            NewSupplier("Mindanao Auto Parts", "Tagum City", now),
            NewSupplier("Southern Lubricants", "General Santos City", now)
        };
        var purchaseOrders = NewPurchaseOrders(suppliers, itemMap, actor.Id, now);
        var borrowSeed = NewBorrowRequests(
            itemMap,
            actor.Id,
            new[] { borrowerA.Id, borrowerB.Id, borrowerC.Id, borrowerD.Id },
            now);

        _dbContext.InventoryItems.AddRange(items);
        _dbContext.Suppliers.AddRange(suppliers);
        _dbContext.PurchaseOrders.AddRange(purchaseOrders.Orders);
        _dbContext.PurchaseOrderLines.AddRange(purchaseOrders.Lines);
        _dbContext.PurchaseOrderReceipts.AddRange(purchaseOrders.Receipts);
        _dbContext.Requests.AddRange(borrowSeed.Requests);
        _dbContext.RequestLines.AddRange(borrowSeed.RequestLines);
        _dbContext.Loans.AddRange(borrowSeed.Loans);
        _dbContext.LoanLines.AddRange(borrowSeed.LoanLines);
        _dbContext.LoanLineReturns.AddRange(borrowSeed.Returns);
        _dbContext.StockLogs.AddRange(items.Select(item => new StockLog
        {
            Id = Guid.NewGuid(),
            InventoryId = item.Id,
            MovementType = StockMovementType.In,
            QtyDelta = item.Quantity,
            UnitCostSnapshot = item.AverageCost,
            TotalCostSnapshot = item.Quantity * item.AverageCost,
            RefType = "demo-opening-balance",
            RefId = item.Id,
            ActorUserId = actor.Id,
            MetaJson = "{\"source\":\"demo reseed\"}",
            CreatedAt = now.AddDays(-92)
        })
        .Concat(purchaseOrders.StockLogs)
        .Concat(borrowSeed.StockLogs));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static User NewUser(string username, string email, string passwordHash, DateTime createdAt)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = createdAt
        };
    }

    private static UserRole NewUserRole(User user, int roleId)
    {
        return new UserRole
        {
            UserId = user.Id,
            RoleId = roleId
        };
    }

    private static Asset NewAsset(string assetCode, AssetType assetType, string plateNo, DateTime createdAt)
    {
        return new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetType = assetType,
            PlateNo = plateNo,
            Status = AssetStatus.Active,
            CreatedAt = createdAt
        };
    }

    private static Driver NewDriver(User user, string licenseNumber, DateTime createdAt)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            LicenseNumber = licenseNumber,
            Status = "Active",
            CreatedAt = createdAt
        };
    }

    private static Truck NewTruck(Asset asset, string containerCapability, DateTime createdAt)
    {
        return new Truck
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            PlateNumber = asset.PlateNo ?? asset.AssetCode,
            ContainerCapability = containerCapability,
            Status = "Active",
            CreatedAt = createdAt
        };
    }

    private static Trailer NewTrailer(Asset asset, string containerType, DateTime createdAt)
    {
        return new Trailer
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            TrailerCode = asset.AssetCode,
            ContainerType = containerType,
            Status = "Active",
            CreatedAt = createdAt
        };
    }

    private static IReadOnlyList<TripSpec> GenerateTripSpecs(
        IReadOnlyList<Customer> customers,
        IReadOnlyList<User> drivers,
        IReadOnlyList<Asset> trucks,
        DateTime now)
    {
        var routes = new[]
        {
            new RouteSpec("DICT Compound, Tagum", "KTC Compound, Davao City", "Medium", customers[1]),
            new RouteSpec("DICT Compound, Tagum", "TADECO Dole, Panabo", "Short", customers[0]),
            new RouteSpec("KUDOS Logistics, Tagum", "Manila South Harbor", "Long", customers[0]),
            new RouteSpec("KTC Compound, Davao City", "DICT Compound, Tagum", "Medium", customers[1]),
            new RouteSpec("Manila South Harbor", "DICT Compound, Tagum", "Long", customers[0]),
            new RouteSpec("DICT Compound, Tagum", "Bunawan, Davao City", "Medium", customers[1]),
            new RouteSpec("KUDOS Logistics, Tagum", "Calinan, Davao City", "Medium", customers[0]),
            new RouteSpec("DICT Compound, Tagum", "Sto. Tomas, Davao del Norte", "Short", customers[0])
        };
        var weekTripCounts = new[] { 9, 9, 9, 9, 13, 13, 13, 15, 15, 15 };
        var statuses = Enumerable.Repeat(TripStatus.Delivered, 84)
            .Concat(Enumerable.Repeat(TripStatus.EnrouteDropoff, 12))
            .Concat(Enumerable.Repeat(TripStatus.Loaded, 10))
            .Concat(Enumerable.Repeat(TripStatus.OnHold, 6))
            .Concat(Enumerable.Repeat(TripStatus.FailedAttempt, 5))
            .Concat(Enumerable.Repeat(TripStatus.Draft, 3))
            .ToArray();
        var shippingLines = new[] { "EVERGREEN", "MAERSK", "COSCO", "PIL", "ONE" };
        var containerPrefixes = new[] { "TCKU", "MSCU", "EISU", "EGHU", "PONU", "ONEY", "COSU", "SEGU" };

        var currentWeekStart = GetUtcWeekStart(now.Date);
        var firstWeekStart = currentWeekStart.AddDays(-63);
        var specs = new List<TripSpec>(120);
        var sequence = 0;

        for (var weekIndex = 0; weekIndex < weekTripCounts.Length; weekIndex++)
        {
            var weekStart = firstWeekStart.AddDays(weekIndex * 7);
            var tripsThisWeek = weekTripCounts[weekIndex];

            for (var index = 0; index < tripsThisWeek; index++)
            {
                sequence++;
                var route = routes[(sequence - 1) % routes.Length];
                var driverIndex = (sequence - 1) % drivers.Count;
                var pickupAt = weekStart
                    .AddDays(index % 6)
                    .AddHours(6 + index % 5)
                    .AddMinutes((index % 4) * 10);
                var scheduledDropoffAt = pickupAt.AddHours(route.DistanceBand == "Short" ? 2 : route.DistanceBand == "Medium" ? 4 : 6);
                var actualPickupAt = pickupAt.AddMinutes(((sequence % 7) - 3) * 10);
                var actualDropoffAt = scheduledDropoffAt.AddMinutes(((sequence % 7) - 3) * 15);
                var rate = CalculateDemoRate(route.DistanceBand, sequence);
                var payroll = Math.Round(rate * (0.35m + (sequence % 6) * 0.01m), 2);
                var fuelAmount = route.DistanceBand == "Short"
                    ? 20m + sequence % 11
                    : route.DistanceBand == "Medium"
                        ? 34m + sequence % 17
                        : 50m + sequence % 11;

                specs.Add(new TripSpec(
                    route.Customer,
                    drivers[driverIndex],
                    trucks[driverIndex],
                    statuses[sequence - 1],
                    route.Pickup,
                    route.Dropoff,
                    pickupAt,
                    scheduledDropoffAt,
                    actualPickupAt,
                    actualDropoffAt,
                    BuildContainerNumber(containerPrefixes[(sequence - 1) % containerPrefixes.Length], sequence),
                    $"NVG-2025-{sequence:00000}",
                    $"EIR-2025-{sequence:00000}",
                    $"BK-2025-{sequence:00000}",
                    shippingLines[(sequence - 1) % shippingLines.Length],
                    sequence % 10 < 7 ? "TwentyFt" : "FortyFt",
                    sequence % 20 < 12 ? "PortPickup" : sequence % 20 < 17 ? "PortDropoff" : "YardTransfer",
                    rate,
                    payroll,
                    200m + (sequence % 7) * 50m,
                    fuelAmount,
                    58m + sequence % 8,
                    $"OR-2025-{sequence:00000}"));
            }
        }

        return specs;
    }

    private static decimal CalculateDemoRate(string distanceBand, int sequence)
    {
        return distanceBand == "Short"
            ? 2500m + (sequence % 11) * 100m
            : distanceBand == "Medium"
                ? 3500m + (sequence % 21) * 100m
                : 6500m + (sequence % 21) * 100m;
    }

    private static DateTime GetUtcWeekStart(DateTime today)
    {
        var offset = today.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : (int)today.DayOfWeek - (int)DayOfWeek.Monday;
        return today.AddDays(-offset);
    }

    private static string BuildContainerNumber(string prefix, int sequence)
    {
        var numeric = 300000 + sequence * 7919 % 699999;
        var checkDigit = sequence % 10;
        return $"{prefix}{numeric:000000}{checkDigit}";
    }

    private static Trip NewTrip(TripSpec spec, User dispatcher, User manager, DateTime now)
    {
        var completedAt = spec.Status == TripStatus.Delivered ? spec.ActualDropoffAt : (DateTime?)null;
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = spec.Customer.Id,
            DriverUserId = spec.Driver?.Id,
            TruckAssetId = spec.Truck?.Id,
            Status = spec.Status,
            PodPending = false,
            Notes = $"Demo dispatch: {spec.Pickup} to {spec.Dropoff}",
            ContainerNumber = spec.ContainerNumber,
            WaybillNumber = spec.WaybillNumber,
            EirNumber = spec.EirNumber,
            BookingNumber = spec.BookingNumber,
            ShippingLine = spec.ShippingLine,
            ContainerSize = spec.ContainerSize,
            TripType = spec.TripType,
            Rate = spec.Rate,
            Payroll = spec.Payroll,
            Allowance = spec.Allowance,
            FuelAmount = spec.FuelAmount,
            FuelPricePerLiter = spec.FuelPricePerLiter,
            OfficialReceiptNumber = spec.OfficialReceiptNumber,
            CreatedAt = spec.ScheduledPickupAt.AddHours(-3),
            UpdatedAt = completedAt ?? now
        };

        trip.Stops.Add(new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            StopType = TripStopType.Pickup,
            LocationText = spec.Pickup,
            ScheduledAt = spec.ScheduledPickupAt,
            ActualAt = HasReachedPickup(spec.Status) ? spec.ActualPickupAt : null,
            CreatedAt = spec.ScheduledPickupAt.AddHours(-3)
        });
        trip.Stops.Add(new TripStop
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            StopType = TripStopType.Dropoff,
            LocationText = spec.Dropoff,
            ScheduledAt = spec.ScheduledDropoffAt,
            ActualAt = spec.Status == TripStatus.Delivered ? spec.ActualDropoffAt : null,
            CreatedAt = spec.ScheduledPickupAt.AddHours(-3)
        });

        foreach (var history in NewHistory(trip.Id, spec, dispatcher.Id, manager.Id, now))
        {
            trip.StatusHistory.Add(history);
        }

        return trip;
    }

    private static bool HasReachedPickup(TripStatus status)
    {
        return status is TripStatus.AtPickup
            or TripStatus.Loaded
            or TripStatus.EnrouteDropoff
            or TripStatus.AtDropoff
            or TripStatus.Delivered
            or TripStatus.Closed
            or TripStatus.OnHold
            or TripStatus.FailedAttempt;
    }

    private static IEnumerable<TripStatusHistory> NewHistory(
        Guid tripId,
        TripSpec spec,
        Guid dispatcherId,
        Guid managerId,
        DateTime now)
    {
        var statuses = new[]
        {
            TripStatus.Draft,
            TripStatus.Dispatched,
            TripStatus.EnroutePickup,
            TripStatus.AtPickup,
            TripStatus.Loaded,
            TripStatus.EnrouteDropoff,
            TripStatus.AtDropoff,
            TripStatus.Delivered,
            TripStatus.Closed
        };

        if (spec.Status == TripStatus.Draft)
        {
            return [NewHistoryRow(tripId, TripStatus.Draft, TripStatus.Draft, dispatcherId, spec.ScheduledPickupAt.AddHours(-3), now, "Draft shipment created.")];
        }

        if (spec.Status == TripStatus.OnHold)
        {
            return BuildHistoryRows(tripId, statuses, TripStatus.Loaded, dispatcherId, managerId, spec, now)
                .Append(NewHistoryRow(tripId, TripStatus.Loaded, TripStatus.OnHold, managerId, spec.ScheduledPickupAt.AddHours(2.5), now, "Placed on hold awaiting gate clearance."))
                .ToList();
        }

        if (spec.Status == TripStatus.FailedAttempt)
        {
            return BuildHistoryRows(tripId, statuses, TripStatus.EnrouteDropoff, dispatcherId, managerId, spec, now)
                .Append(NewHistoryRow(tripId, TripStatus.EnrouteDropoff, TripStatus.FailedAttempt, dispatcherId, spec.ScheduledDropoffAt.AddMinutes(20), now, "Failed attempt due to receiver queue cutoff."))
                .ToList();
        }

        return BuildHistoryRows(tripId, statuses, spec.Status, dispatcherId, managerId, spec, now);
    }

    private static List<TripStatusHistory> BuildHistoryRows(
        Guid tripId,
        IReadOnlyList<TripStatus> statuses,
        TripStatus finalStatus,
        Guid dispatcherId,
        Guid managerId,
        TripSpec spec,
        DateTime now)
    {
        var finalIndex = Array.IndexOf(statuses.ToArray(), finalStatus);
        var rows = new List<TripStatusHistory>();
        for (var index = 0; index <= finalIndex; index++)
        {
            var fromStatus = index == 0 ? TripStatus.Draft : statuses[index - 1];
            var toStatus = statuses[index];
            var eventAt = toStatus switch
            {
                TripStatus.Draft => spec.ScheduledPickupAt.AddHours(-3),
                TripStatus.Dispatched => spec.ScheduledPickupAt.AddHours(-2),
                TripStatus.EnroutePickup => spec.ScheduledPickupAt.AddMinutes(-45),
                TripStatus.AtPickup => spec.ActualPickupAt,
                TripStatus.Loaded => spec.ActualPickupAt.AddMinutes(55),
                TripStatus.EnrouteDropoff => spec.ActualPickupAt.AddMinutes(85),
                TripStatus.AtDropoff => spec.ActualDropoffAt.AddMinutes(-25),
                TripStatus.Delivered => spec.ActualDropoffAt,
                TripStatus.Closed => spec.ActualDropoffAt.AddHours(1),
                _ => spec.ScheduledPickupAt.AddMinutes(index * 45)
            };
            rows.Add(NewHistoryRow(
                tripId,
                fromStatus,
                toStatus,
                index == finalIndex && finalStatus == TripStatus.Closed ? managerId : dispatcherId,
                eventAt,
                now,
                index == 0 ? "Draft created." : $"{toStatus} recorded."));
        }

        return rows;
    }

    private static TripStatusHistory NewHistoryRow(
        Guid tripId,
        TripStatus fromStatus,
        TripStatus toStatus,
        Guid actorUserId,
        DateTime eventAt,
        DateTime now,
        string remarks)
    {
        return new TripStatusHistory
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            Remarks = remarks,
            EventAt = eventAt,
            RecordedAt = eventAt > now ? now : eventAt.AddMinutes(3)
        };
    }

    private static IEnumerable<TripDocument> NewTripDocuments(
        IReadOnlyList<Trip> trips,
        User dispatcher,
        User manager)
    {
        foreach (var trip in trips.Where(trip => trip.Status is not TripStatus.Draft and not TripStatus.FailedAttempt))
        {
            var uploadedAt = trip.CreatedAt.AddHours(1);
            var driverUserId = trip.DriverUserId ?? dispatcher.Id;
            if (trip.Status == TripStatus.Delivered)
            {
                yield return NewTripDocument(trip.Id, TripDocumentType.Atw, TripDocumentState.Verified, dispatcher.Id, manager.Id, uploadedAt);
                yield return NewTripDocument(trip.Id, TripDocumentType.Eir, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddMinutes(15));
                yield return NewTripDocument(trip.Id, TripDocumentType.GatePass, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddMinutes(20));
                yield return NewTripDocument(trip.Id, TripDocumentType.Dr, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddHours(4));
                yield return NewTripDocument(trip.Id, TripDocumentType.Pod, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddHours(5));
                continue;
            }

            if (trip.Status is TripStatus.EnrouteDropoff or TripStatus.Loaded)
            {
                yield return NewTripDocument(trip.Id, TripDocumentType.Atw, TripDocumentState.Verified, dispatcher.Id, manager.Id, uploadedAt);
                yield return NewTripDocument(trip.Id, TripDocumentType.Eir, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddMinutes(15));
                yield return NewTripDocument(trip.Id, TripDocumentType.GatePass, TripDocumentState.Verified, driverUserId, manager.Id, uploadedAt.AddMinutes(20));
                continue;
            }

            if (trip.Status == TripStatus.OnHold)
            {
                var atwState = trip.Id.ToByteArray()[0] % 2 == 0 ? TripDocumentState.Missing : TripDocumentState.Rejected;
                yield return NewTripDocument(
                    trip.Id,
                    TripDocumentType.Atw,
                    atwState,
                    dispatcher.Id,
                    null,
                    uploadedAt,
                    rejectedByUserId: atwState == TripDocumentState.Rejected ? manager.Id : null,
                    rejectedAt: uploadedAt.AddMinutes(40),
                    remarks: "Incomplete details");
            }
        }
    }

    private static TripDocument NewTripDocument(
        Guid tripId,
        TripDocumentType type,
        TripDocumentState state,
        Guid uploadedByUserId,
        Guid? verifiedByUserId,
        DateTime uploadedAt,
        Guid? supersedesDocumentId = null,
        bool isActive = true,
        Guid? rejectedByUserId = null,
        DateTime? rejectedAt = null,
        string? remarks = null)
    {
        return new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            Type = type,
            State = state,
            SupersedesDocumentId = supersedesDocumentId,
            IsActive = isActive,
            StorageKey = $"nvg-dispatch/documents/demo/{tripId}/{type.ToString().ToLowerInvariant()}-{uploadedAt:yyyyMMddHHmm}.jpg",
            UploadedByUserId = uploadedByUserId,
            VerifiedByUserId = verifiedByUserId,
            RejectedByUserId = rejectedByUserId,
            UploadedAt = uploadedAt,
            VerifiedAt = state == TripDocumentState.Verified ? uploadedAt.AddMinutes(40) : null,
            RejectedAt = state == TripDocumentState.Rejected ? rejectedAt ?? uploadedAt.AddMinutes(40) : null,
            Remarks = remarks ?? (state == TripDocumentState.Verified ? "Demo verified document." : "Demo document pending review.")
        };
    }

    private static IEnumerable<GeneratedWaybill> NewGeneratedWaybills(
        IReadOnlyList<Trip> trips,
        User dispatcher)
    {
        var sequence = 1;
        foreach (var trip in trips.Where(trip => trip.Status == TripStatus.Delivered))
        {
            var generatedAt = (trip.UpdatedAt ?? trip.CreatedAt).AddMinutes(20);
            var waybillNumber = $"NVG-{generatedAt.Year}-{sequence:00000}";
            sequence++;

            yield return new GeneratedWaybill
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                WaybillNumber = waybillNumber,
                Version = 1,
                GeneratedAt = generatedAt,
                GeneratedByUserId = dispatcher.Id,
                IsActive = true,
                WaybillDataJson = JsonSerializer.Serialize(new
                {
                    WaybillNumber = waybillNumber,
                    trip.ContainerNumber,
                    trip.EirNumber,
                    trip.BookingNumber,
                    trip.ShippingLine,
                    DriverName = trip.Driver?.Username,
                    TruckPlate = trip.TruckAsset?.PlateNo ?? trip.TruckAsset?.AssetCode,
                    CustomerName = trip.Customer?.Name,
                    trip.Rate,
                    GeneratedAt = generatedAt,
                    GeneratedByUserId = dispatcher.Id,
                    Version = 1
                })
            };
        }
    }

    private static IEnumerable<DispatchRecommendation> NewDispatchRecommendations(
        IReadOnlyList<Trip> trips,
        User dispatcher,
        DateTime now)
    {
        var completedTrips = trips
            .Where(trip => trip.Status == TripStatus.Delivered && trip.DriverUserId.HasValue && trip.TruckAssetId.HasValue)
            .Take(5)
            .ToList();
        var candidateTrips = trips
            .Where(trip => trip.Status == TripStatus.Draft)
            .Take(3)
            .ToList();
        if (completedTrips.Count < 5 || candidateTrips.Count < 3)
        {
            yield break;
        }

        var scoreSets = new[]
        {
            new[] { 0.88m, 0.74m, 0.61m },
            new[] { 0.83m, 0.69m, 0.55m },
            new[] { 0.79m, 0.64m, 0.47m },
            new[] { 0.72m, 0.58m, 0.44m },
            new[] { 0.68m, 0.53m, 0.41m }
        };

        for (var groupIndex = 0; groupIndex < completedTrips.Count; groupIndex++)
        {
            var completedTrip = completedTrips[groupIndex];
            var generatedAt = now.AddDays(-7 * (groupIndex % 4)).AddHours(-groupIndex - 2);
            var groupAccepted = groupIndex < 2;

            for (var rank = 1; rank <= 3; rank++)
            {
                var totalScore = scoreSets[groupIndex][rank - 1];
                var wasAccepted = groupAccepted && rank == 1;
                var wasIgnored = !wasAccepted;

                yield return new DispatchRecommendation
                {
                    Id = Guid.NewGuid(),
                    CompletedTripId = completedTrip.Id,
                    DriverId = completedTrip.DriverUserId!.Value,
                    TruckId = completedTrip.TruckAssetId!.Value,
                    RecommendedTripId = candidateTrips[rank - 1].Id,
                    ProximityScore = Math.Min(1m, totalScore + 0.06m),
                    AvailabilityScore = Math.Max(0.2m, totalScore - 0.04m),
                    TruckMatchScore = rank == 1 ? 1.0m : 0.7m,
                    AgingScore = rank == 3 ? 0.8m : 0.5m,
                    TotalScore = totalScore,
                    Rank = rank,
                    GeneratedAt = generatedAt,
                    ExpiresAt = generatedAt.AddMinutes(30),
                    WasAccepted = wasAccepted,
                    WasIgnored = wasIgnored,
                    ReviewedByUserId = dispatcher.Id,
                    ReviewedAt = generatedAt.AddMinutes(12 + rank)
                };
            }
        }
    }

    private static IReadOnlyCollection<ShipmentRequest> GenerateShipmentRequests(
        IReadOnlyList<Customer> customers,
        IReadOnlyList<User> customerUsers,
        IReadOnlyList<Trip> trips,
        Guid approvedByUserId,
        DateTime now)
    {
        var requests = new List<ShipmentRequest>(43);
        var cargoDescriptions = new[]
        {
            "Banana export cartons for reefer loading",
            "Empty container repositioning",
            "Palletized consumer goods",
            "Agricultural inputs and packaging materials",
            "Cold-chain mixed cargo",
            "Port pull-out for client warehouse delivery"
        };
        var statuses = Enumerable.Repeat(ShipmentRequestStatus.Draft, 5)
            .Concat(Enumerable.Repeat(ShipmentRequestStatus.Submitted, 8))
            .Concat(Enumerable.Repeat(ShipmentRequestStatus.Approved, 6))
            .Concat(Enumerable.Repeat(ShipmentRequestStatus.ConvertedToTrip, 20))
            .Concat(Enumerable.Repeat(ShipmentRequestStatus.Rejected, 4))
            .ToArray();
        var convertedTrips = trips.Take(20).ToArray();

        for (var index = 0; index < statuses.Length; index++)
        {
            var customerIndex = index % customers.Count;
            var status = statuses[index];
            var convertedTripId = status == ShipmentRequestStatus.ConvertedToTrip
                ? convertedTrips[index - 19].Id
                : (Guid?)null;

            requests.Add(NewShipmentRequest(
                customers[customerIndex],
                customerUsers[customerIndex],
                status,
                now.Date.AddDays(index - 20).AddHours(8 + index % 3),
                cargoDescriptions[index % cargoDescriptions.Length],
                index % 10 < 7 ? "TwentyFt" : "FortyFt",
                index % 20 < 12 ? "PortPickup" : index % 20 < 17 ? "PortDropoff" : "YardTransfer",
                convertedTripId,
                status is ShipmentRequestStatus.Approved or ShipmentRequestStatus.ConvertedToTrip ? approvedByUserId : null,
                status == ShipmentRequestStatus.Rejected ? RejectionRemark(index) : null));
        }

        return requests;
    }

    private static string RejectionRemark(int index)
    {
        var remarks = new[]
        {
            "Requested pickup window conflicts with vessel gate cutoff.",
            "Missing booking confirmation from shipping line.",
            "Cargo weight exceeds declared container plan.",
            "Client asked to revise delivery address before dispatch."
        };

        return remarks[index % remarks.Length];
    }

    private static ShipmentRequest NewShipmentRequest(
        Customer customer,
        User createdBy,
        ShipmentRequestStatus status,
        DateTime requestedPickupTime,
        string cargoDescription,
        string containerSize,
        string tripType,
        Guid? convertedTripId = null,
        Guid? approvedByUserId = null,
        string? rejectionRemarks = null)
    {
        return new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = status,
            PickupLocation = customer.Name.Contains("Cold", StringComparison.OrdinalIgnoreCase)
                ? "Bunawan District, Davao City"
                : "Panabo City, Davao del Norte",
            DropoffLocation = "Davao International Container Terminal / Sasa Wharf",
            RequestedPickupTime = requestedPickupTime,
            CargoDescription = cargoDescription,
            CargoWeight = 18500m,
            ContainerSize = containerSize,
            TripType = tripType,
            SpecialInstructions = "Coordinate gate pass and container seal before dispatch.",
            RejectionRemarks = rejectionRemarks,
            CreatedAt = requestedPickupTime.AddDays(-2),
            CreatedByUserId = createdBy.Id,
            ApprovedAt = approvedByUserId.HasValue ? requestedPickupTime.AddDays(-1) : null,
            ApprovedByUserId = approvedByUserId,
            ConvertedTripId = convertedTripId
        };
    }

    private static InventoryItem NewInventoryItem(
        string name,
        string unit,
        ItemType itemType,
        decimal quantity,
        decimal reorderLevel,
        decimal averageCost,
        string location,
        DateTime now)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Unit = unit,
            ItemType = itemType,
            Quantity = quantity,
            ReorderLevel = reorderLevel,
            AverageCost = averageCost,
            LastCost = averageCost,
            UnitValue = averageCost,
            Location = location,
            IsActive = true,
            CreatedAt = now.AddDays(-10)
        };
    }

    private static Supplier NewSupplier(string name, string address, DateTime now)
    {
        return new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            ContactName = $"{name} Sales Desk",
            ContactPhone = "+63 82 555 01" + Math.Abs(name.GetHashCode() % 90 + 10).ToString("00"),
            ContactEmail = name.ToLowerInvariant().Replace(" ", ".") + "@example.local",
            Address = address,
            IsActive = true,
            CreatedAt = now.AddDays(-95)
        };
    }

    private static PurchaseOrderSeed NewPurchaseOrders(
        IReadOnlyList<Supplier> suppliers,
        IReadOnlyDictionary<string, InventoryItem> items,
        Guid actorUserId,
        DateTime now)
    {
        var seedLines = new[]
        {
            new PurchaseOrderSpec(suppliers[0], PurchaseOrderStatus.Closed, now.AddDays(-82), "Received engine service consumables",
            [
                new PurchaseLineSpec(items["Engine Oil (SAE 40)"], 30m, 290m),
                new PurchaseLineSpec(items["Fuel Filter"], 8m, 650m)
            ]),
            new PurchaseOrderSpec(suppliers[1], PurchaseOrderStatus.Approved, now.AddDays(-64), "Approved brake and tire replenishment",
            [
                new PurchaseLineSpec(items["Brake Pads (Heavy Duty)"], 10m, 1850m),
                new PurchaseLineSpec(items["Tire (10.00R20)"], 1m, 14500m)
            ]),
            new PurchaseOrderSpec(suppliers[2], PurchaseOrderStatus.Closed, now.AddDays(-47), "Received lubricants for Panabo maintenance",
            [
                new PurchaseLineSpec(items["Hydraulic Oil"], 60m, 245m),
                new PurchaseLineSpec(items["Grease (Lithium)"], 40m, 180m)
            ]),
            new PurchaseOrderSpec(suppliers[0], PurchaseOrderStatus.Draft, now.AddDays(-29), "Draft filters and coolant order",
            [
                new PurchaseLineSpec(items["Air Filter"], 8m, 780m),
                new PurchaseLineSpec(items["Coolant"], 12m, 190m)
            ]),
            new PurchaseOrderSpec(suppliers[1], PurchaseOrderStatus.Approved, now.AddDays(-18), "Approved urgent low-stock parts",
            [
                new PurchaseLineSpec(items["Brake Pads (Heavy Duty)"], 8m, 1900m),
                new PurchaseLineSpec(items["Fuel Filter"], 6m, 675m)
            ]),
            new PurchaseOrderSpec(suppliers[2], PurchaseOrderStatus.Closed, now.AddDays(-7), "Received oil top-up before long-haul dispatches",
            [
                new PurchaseLineSpec(items["Engine Oil (SAE 40)"], 40m, 288m),
                new PurchaseLineSpec(items["Coolant"], 20m, 195m)
            ])
        };

        var orders = new List<PurchaseOrder>(seedLines.Length);
        var lines = new List<PurchaseOrderLine>();
        var receipts = new List<PurchaseOrderReceipt>();
        var stockLogs = new List<StockLog>();

        foreach (var spec in seedLines)
        {
            var order = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                SupplierId = spec.Supplier.Id,
                Notes = spec.Notes,
                Status = spec.Status,
                CreatedByUserId = actorUserId,
                CreatedAt = spec.CreatedAt,
                SubmittedAt = spec.Status == PurchaseOrderStatus.Draft ? null : spec.CreatedAt.AddHours(2),
                ApprovedAt = spec.Status == PurchaseOrderStatus.Draft ? null : spec.CreatedAt.AddDays(1),
                ReceivedAt = spec.Status == PurchaseOrderStatus.Closed ? spec.CreatedAt.AddDays(4) : null,
                ClosedAt = spec.Status == PurchaseOrderStatus.Closed ? spec.CreatedAt.AddDays(4).AddHours(1) : null,
                UpdatedAt = spec.Status == PurchaseOrderStatus.Closed ? spec.CreatedAt.AddDays(4).AddHours(1) : spec.CreatedAt.AddDays(1)
            };
            orders.Add(order);

            foreach (var specLine in spec.Lines)
            {
                var line = new PurchaseOrderLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = order.Id,
                    InventoryId = specLine.Item.Id,
                    QtyOrdered = specLine.Quantity,
                    QtyReceived = spec.Status == PurchaseOrderStatus.Closed ? specLine.Quantity : 0m,
                    UnitPrice = specLine.UnitPrice,
                    Remarks = spec.Status == PurchaseOrderStatus.Closed ? "Received in good condition." : null
                };
                lines.Add(line);

                if (spec.Status != PurchaseOrderStatus.Closed)
                {
                    continue;
                }

                var receivedAt = order.ReceivedAt ?? spec.CreatedAt.AddDays(4);
                receipts.Add(new PurchaseOrderReceipt
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderLineId = line.Id,
                    QtyReceivedIncrement = specLine.Quantity,
                    ReceivedByUserId = actorUserId,
                    ReceivedAt = receivedAt
                });
                stockLogs.Add(NewStockLog(
                    StockMovementType.In,
                    specLine.Item,
                    specLine.Quantity,
                    specLine.UnitPrice,
                    EntityTypes.PurchaseOrder,
                    order.Id,
                    actorUserId,
                    receivedAt,
                    "{\"source\":\"demo purchase receipt\"}"));
            }
        }

        return new PurchaseOrderSeed(orders, lines, receipts, stockLogs);
    }

    private static BorrowRequestSeed NewBorrowRequests(
        IReadOnlyDictionary<string, InventoryItem> items,
        Guid actorUserId,
        IReadOnlyList<Guid> borrowerUserIds,
        DateTime now)
    {
        var specs = new[]
        {
            new BorrowSpec(borrowerUserIds[0], items["Air Filter"], 2m, false, now.AddDays(-21), "Air filter replacement for NVG-1001"),
            new BorrowSpec(borrowerUserIds[1], items["Grease (Lithium)"], 4m, false, now.AddDays(-15), "Chassis lubrication kit for Panabo run"),
            new BorrowSpec(borrowerUserIds[2], items["Engine Oil (SAE 40)"], 6m, true, now.AddDays(-37), "Oil top-up before Tagum dispatch"),
            new BorrowSpec(borrowerUserIds[3], items["Coolant"], 2m, true, now.AddDays(-10), "Coolant issued for Davao yard inspection")
        };

        var requests = new List<Request>(specs.Length);
        var requestLines = new List<RequestLine>(specs.Length);
        var loans = new List<Loan>(specs.Length);
        var loanLines = new List<LoanLine>(specs.Length);
        var returns = new List<LoanLineReturn>();
        var stockLogs = new List<StockLog>();

        foreach (var spec in specs)
        {
            var request = new Request
            {
                Id = Guid.NewGuid(),
                RequestType = RequestType.Borrow,
                RequesterUserId = spec.BorrowerUserId,
                Purpose = spec.Purpose,
                Status = spec.Returned ? RequestStatus.Closed : RequestStatus.Issued,
                SubmittedAt = spec.CreatedAt.AddHours(1),
                ApprovedAt = spec.CreatedAt.AddHours(4),
                IssuedAt = spec.CreatedAt.AddHours(6),
                ClosedAt = spec.Returned ? spec.CreatedAt.AddDays(2) : null,
                CreatedAt = spec.CreatedAt,
                UpdatedAt = spec.Returned ? spec.CreatedAt.AddDays(2) : spec.CreatedAt.AddHours(6)
            };
            requests.Add(request);

            requestLines.Add(new RequestLine
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                InventoryId = spec.Item.Id,
                QtyRequested = spec.Quantity,
                QtyApproved = spec.Quantity,
                Remarks = spec.Purpose,
                CreatedAt = spec.CreatedAt
            });

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                BorrowerUserId = spec.BorrowerUserId,
                Status = spec.Returned ? LoanStatus.Closed : LoanStatus.Open,
                IssuedAt = spec.CreatedAt.AddHours(6),
                DueAt = spec.CreatedAt.AddDays(7),
                ClosedAt = spec.Returned ? spec.CreatedAt.AddDays(2) : null
            };
            loans.Add(loan);

            var loanLine = new LoanLine
            {
                Id = Guid.NewGuid(),
                LoanId = loan.Id,
                InventoryId = spec.Item.Id,
                QtyIssued = spec.Quantity,
                QtyReturned = spec.Returned ? spec.Quantity : 0m
            };
            loanLines.Add(loanLine);

            stockLogs.Add(NewStockLog(
                StockMovementType.Borrow,
                spec.Item,
                -spec.Quantity,
                spec.Item.AverageCost,
                EntityTypes.Request,
                request.Id,
                actorUserId,
                loan.IssuedAt,
                "{\"source\":\"demo borrow issue\"}"));

            if (!spec.Returned)
            {
                continue;
            }

            var returnedAt = spec.CreatedAt.AddDays(2);
            returns.Add(new LoanLineReturn
            {
                Id = Guid.NewGuid(),
                LoanLineId = loanLine.Id,
                QtyReturned = spec.Quantity,
                Condition = ReturnCondition.Good,
                ReceivedByUserId = actorUserId,
                ReturnedAt = returnedAt
            });
            stockLogs.Add(NewStockLog(
                StockMovementType.Return,
                spec.Item,
                spec.Quantity,
                spec.Item.AverageCost,
                EntityTypes.Loan,
                loan.Id,
                actorUserId,
                returnedAt,
                "{\"source\":\"demo borrow return\"}"));
        }

        return new BorrowRequestSeed(requests, requestLines, loans, loanLines, returns, stockLogs);
    }

    private static StockLog NewStockLog(
        StockMovementType movementType,
        InventoryItem item,
        decimal qtyDelta,
        decimal? unitCost,
        string refType,
        Guid refId,
        Guid actorUserId,
        DateTime createdAt,
        string metaJson)
    {
        return new StockLog
        {
            Id = Guid.NewGuid(),
            MovementType = movementType,
            InventoryId = item.Id,
            QtyDelta = qtyDelta,
            UnitCostSnapshot = unitCost,
            TotalCostSnapshot = unitCost.HasValue ? Math.Abs(qtyDelta) * unitCost.Value : null,
            RefType = refType,
            RefId = refId,
            ActorUserId = actorUserId,
            MetaJson = metaJson,
            CreatedAt = createdAt
        };
    }

    private async Task LogSeedSummaryAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users.CountAsync(cancellationToken);
        var trips = await _dbContext.DispatchTrips.CountAsync(cancellationToken);
        var documents = await _dbContext.DispatchTripDocuments.CountAsync(cancellationToken);
        var shipmentRequests = await _dbContext.ShipmentRequests.CountAsync(cancellationToken);
        var inventoryItems = await _dbContext.InventoryItems.CountAsync(cancellationToken);

        var message = "Demo seed complete: "
                      + $"users={users}, trips={trips}, documents={documents}, "
                      + $"shipmentRequests={shipmentRequests}, inventoryItems={inventoryItems}.";
        _logger.LogInformation("{Message}", message);
        Console.WriteLine(message);
    }

    private sealed record TripSpec(
        Customer Customer,
        User? Driver,
        Asset? Truck,
        TripStatus Status,
        string Pickup,
        string Dropoff,
        DateTime ScheduledPickupAt,
        DateTime ScheduledDropoffAt,
        DateTime ActualPickupAt,
        DateTime ActualDropoffAt,
        string ContainerNumber,
        string WaybillNumber,
        string EirNumber,
        string BookingNumber,
        string ShippingLine,
        string ContainerSize,
        string TripType,
        decimal? Rate,
        decimal? Payroll,
        decimal? Allowance,
        decimal? FuelAmount,
        decimal? FuelPricePerLiter,
        string? OfficialReceiptNumber);

    private sealed record RouteSpec(
        string Pickup,
        string Dropoff,
        string DistanceBand,
        Customer Customer);

    private sealed record PurchaseLineSpec(InventoryItem Item, decimal Quantity, decimal UnitPrice);

    private sealed record PurchaseOrderSpec(
        Supplier Supplier,
        PurchaseOrderStatus Status,
        DateTime CreatedAt,
        string Notes,
        IReadOnlyList<PurchaseLineSpec> Lines);

    private sealed record PurchaseOrderSeed(
        IReadOnlyCollection<PurchaseOrder> Orders,
        IReadOnlyCollection<PurchaseOrderLine> Lines,
        IReadOnlyCollection<PurchaseOrderReceipt> Receipts,
        IReadOnlyCollection<StockLog> StockLogs);

    private sealed record BorrowSpec(
        Guid BorrowerUserId,
        InventoryItem Item,
        decimal Quantity,
        bool Returned,
        DateTime CreatedAt,
        string Purpose);

    private sealed record BorrowRequestSeed(
        IReadOnlyCollection<Request> Requests,
        IReadOnlyCollection<RequestLine> RequestLines,
        IReadOnlyCollection<Loan> Loans,
        IReadOnlyCollection<LoanLine> LoanLines,
        IReadOnlyCollection<LoanLineReturn> Returns,
        IReadOnlyCollection<StockLog> StockLogs);

    private async Task<User> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == SuperAdminUsername, cancellationToken);
        User user;
        if (existing is null)
        {
            user = await _userService.CreateUserAsync(
                new CreateUserCommand(SuperAdminUsername, SuperAdminPassword, SuperAdminEmail),
                cancellationToken);
        }
        else
        {
            user = existing;
        }

        await _userService.AssignRoleAsync(user.Id, RoleNames.SuperAdmin, cancellationToken);
        return user;
    }

    private async Task<Supplier> EnsureSupplierAsync(
        Guid actorUserId,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _supplierService.CreateAsync(
            actorUserId,
            name,
            null,
            null,
            null,
            null,
            cancellationToken);
    }

    private async Task<Asset> EnsureAssetAsync(
        string assetCode,
        AssetType type,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Assets.FirstOrDefaultAsync(a => a.AssetCode == assetCode, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _assetService.CreateAssetAsync(
            new CreateAssetCommand(assetCode, type, null, AssetStatus.Active),
            cancellationToken);
    }

    private async Task<Dictionary<string, InventoryItem>> EnsureInventoryAsync(CancellationToken cancellationToken)
    {
        var items = new List<(string Name, string Unit, ItemType ItemType, decimal Quantity, decimal ReorderLevel, decimal AverageCost)>
        {
            ("Engine Oil", "L", ItemType.Consumable, 120m, 30m, 12.5m),
            ("Brake Fluid", "L", ItemType.Consumable, 60m, 15m, 8m),
            ("Tires", "pcs", ItemType.Consumable, 30m, 8m, 110m),
            ("Wrench Set", "set", ItemType.NonConsumable, 4m, 1m, 300m),
            ("Diagnostic Scanner", "pcs", ItemType.NonConsumable, 2m, 1m, 900m)
        };

        var inventoryMap = new Dictionary<string, InventoryItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var existing = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.Name == item.Name, cancellationToken);
            if (existing is null)
            {
                var created = await _inventoryService.CreateItemAsync(
                    new CreateInventoryItemCommand(
                        item.Name,
                        item.Unit,
                        item.ItemType,
                        0m,
                        item.ReorderLevel,
                        null,
                        null,
                        false),
                    cancellationToken);
                inventoryMap[item.Name] = created;
            }
            else
            {
                inventoryMap[item.Name] = existing;
            }
        }

        return inventoryMap;
    }

    private async Task SeedBaselinePurchaseOrderAsync(
        Guid ioUserId,
        Guid managerId,
        Guid financeId,
        Guid ceoId,
        Guid supplierId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.PurchaseOrders.AnyAsync(po => po.Notes == "DEMO_PO_BASELINE", cancellationToken))
        {
            return;
        }

        var lines = new List<PurchaseOrderLineInput>
        {
            new(inventory["Engine Oil"].Id, 120m, 12.5m, null),
            new(inventory["Brake Fluid"].Id, 60m, 8m, null),
            new(inventory["Tires"].Id, 30m, 110m, null),
            new(inventory["Wrench Set"].Id, 4m, 300m, null),
            new(inventory["Diagnostic Scanner"].Id, 2m, 900m, null)
        };

        var po = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierId,
                "DEMO_PO_BASELINE",
                lines),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po.Id, ceoId, ApprovalDecision.Approve, "Approved", cancellationToken);

        var poLines = await _dbContext.PurchaseOrderLines
            .Where(line => line.PurchaseOrderId == po.Id)
            .ToListAsync(cancellationToken);

        await _purchaseOrderWorkflow.ReceiveAsync(
            po.Id,
            ioUserId,
            poLines.Select(line => new PurchaseOrderReceiveLineInput(line.Id, line.QtyOrdered, null)).ToList(),
            "Baseline receive",
            cancellationToken);
    }

    private async Task SeedPurchaseOrdersAsync(
        Guid ioUserId,
        Guid managerId,
        Guid financeId,
        Guid ceoId,
        Guid supplierAId,
        Guid supplierBId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.PurchaseOrders.AnyAsync(po => po.Notes == "DEMO_PO_PARTIAL", cancellationToken))
        {
            return;
        }

        var po1 = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierAId,
                "DEMO_PO_PARTIAL",
                new[]
                {
                    new PurchaseOrderLineInput(inventory["Engine Oil"].Id, 50m, 14m, null),
                    new PurchaseOrderLineInput(inventory["Brake Fluid"].Id, 30m, 9m, null),
                    new PurchaseOrderLineInput(inventory["Tires"].Id, 10m, 120m, null),
                    new PurchaseOrderLineInput(inventory["Wrench Set"].Id, 2m, 315m, null),
                    new PurchaseOrderLineInput(inventory["Diagnostic Scanner"].Id, 1m, 975m, null)
                }),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po1.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po1.Id, ceoId, ApprovalDecision.Approve, "Approved", cancellationToken);

        var po1Lines = await _dbContext.PurchaseOrderLines
            .Where(line => line.PurchaseOrderId == po1.Id)
            .ToListAsync(cancellationToken);

        var partialReceiptLines = po1Lines
            .Where(line =>
                line.InventoryId == inventory["Engine Oil"].Id ||
                line.InventoryId == inventory["Brake Fluid"].Id ||
                line.InventoryId == inventory["Tires"].Id)
            .Select(line => new PurchaseOrderReceiveLineInput(
                line.Id,
                line.InventoryId == inventory["Engine Oil"].Id ? 25m :
                line.InventoryId == inventory["Brake Fluid"].Id ? 15m : 5m,
                null))
            .ToList();

        await _purchaseOrderWorkflow.ReceiveAsync(
            po1.Id,
            ioUserId,
            partialReceiptLines,
            "Partial receive (3 of 5 items)",
            cancellationToken);

        var po2 = await _purchaseOrderWorkflow.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                ioUserId,
                supplierBId,
                "DEMO_PO_PENDING_CEO",
                new[]
                {
                    new PurchaseOrderLineInput(inventory["Wrench Set"].Id, 2m, 310m, null),
                    new PurchaseOrderLineInput(inventory["Diagnostic Scanner"].Id, 1m, 950m, null)
                }),
            cancellationToken);

        await _purchaseOrderWorkflow.SubmitAsync(po2.Id, ioUserId, cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po2.Id, managerId, ApprovalDecision.Approve, "Approved", cancellationToken);
        await _purchaseOrderWorkflow.ApplyDecisionAsync(po2.Id, financeId, ApprovalDecision.Approve, "Approved", cancellationToken);
    }

    private async Task SeedMaintenanceRequestsAsync(
        Guid requesterId,
        Guid ioUserId,
        Guid managerId,
        Guid assetId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        var hasClosed = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_MAINT_CLOSED", cancellationToken);
        if (!hasClosed)
        {
            var submit = await _requestWorkflow.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requesterId,
                    assetId,
                    "DEMO_MAINT_CLOSED",
                    new[]
                    {
                        new RequestLineInput(inventory["Engine Oil"].Id, 5m, null),
                        new RequestLineInput(inventory["Brake Fluid"].Id, 2m, null)
                    }),
                cancellationToken);

            var request = await _dbContext.Requests
                .Include(r => r.Lines)
                .FirstAsync(r => r.Id == submit.RequestId, cancellationToken);

            await _requestWorkflow.InventoryOfficerReviewAsync(
                request.Id,
                ioUserId,
                request.Lines.Select(line => (line.Id, line.QtyRequested, (string?)null)).ToList(),
                "Approved",
                cancellationToken);

            await _requestWorkflow.ManagerDecisionAsync(
                request.Id,
                managerId,
                ApprovalDecision.Approve,
                "Approved",
                cancellationToken);

            await _requestWorkflow.IssueRequest(request.Id, ioUserId, cancellationToken);
        }

        var hasPendingIo = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_MAINT_PENDING_IO", cancellationToken);
        if (!hasPendingIo)
        {
            await _requestWorkflow.SubmitMaintenanceIssueAsync(
                new SubmitMaintenanceIssueCommand(
                    requesterId,
                    assetId,
                    "DEMO_MAINT_PENDING_IO",
                    new[]
                    {
                        new RequestLineInput(inventory["Brake Fluid"].Id, 5m, null)
                    }),
                cancellationToken);
        }
    }

    private async Task SeedBorrowRequestAsync(
        Guid requesterId,
        Guid ioUserId,
        Guid managerId,
        Guid assetId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        var hasBorrow = await _dbContext.Requests.AnyAsync(r => r.Purpose == "DEMO_BORROW", cancellationToken);
        if (hasBorrow)
        {
            return;
        }

        var draft = await _requestService.CreateRequestAsync(
            new CreateRequestCommand(
                RequestType.Borrow,
                requesterId,
                assetId,
                "DEMO_BORROW",
                new[]
                {
                    new RequestLineInput(inventory["Wrench Set"].Id, 2m, null),
                    new RequestLineInput(inventory["Diagnostic Scanner"].Id, 1m, null)
                }),
            RequestStatus.Draft,
            cancellationToken);

        await _requestWorkflow.SubmitRequest(draft.Id, cancellationToken);

        var request = await _dbContext.Requests
            .Include(r => r.Lines)
            .FirstAsync(r => r.Id == draft.Id, cancellationToken);

        await _requestWorkflow.InventoryOfficerReviewAsync(
            request.Id,
            ioUserId,
            request.Lines.Select(line => (line.Id, line.QtyRequested, (string?)null)).ToList(),
            "Approved",
            cancellationToken);

        await _requestWorkflow.ManagerDecisionAsync(
            request.Id,
            managerId,
            ApprovalDecision.Approve,
            "Approved",
            cancellationToken);

        await _requestWorkflow.IssueRequest(request.Id, ioUserId, cancellationToken);

        var loan = await _dbContext.Loans
            .Include(l => l.Lines)
            .FirstAsync(l => l.RequestId == request.Id, cancellationToken);

        var firstLine = loan.Lines.First();
        await _loanWorkflow.ReturnLoanAsync(
            loan.Id,
            ioUserId,
            new[]
            {
                new ReturnLoanLineInput(firstLine.Id, 1m, ReturnCondition.Good, null)
            },
            cancellationToken);
    }

    private async Task SeedAdjustmentsAsync(
        Guid ioUserId,
        Guid managerId,
        Dictionary<string, InventoryItem> inventory,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.InventoryAdjustments.AnyAsync(a => a.Reason == "DEMO_ADJUSTMENT_SHRINKAGE", cancellationToken))
        {
            var draft = await _adjustmentWorkflow.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "DEMO_ADJUSTMENT_SHRINKAGE",
                    new[]
                    {
                        new InventoryAdjustmentLineInput(inventory["Tires"].Id, -2m, "Shrinkage adjustment")
                    }),
                cancellationToken);

            await _adjustmentWorkflow.SubmitAsync(draft.AdjustmentId, ioUserId, cancellationToken);
            await _adjustmentWorkflow.ApproveAsync(draft.AdjustmentId, managerId, "Approved", cancellationToken);
        }

        if (!await _dbContext.InventoryAdjustments.AnyAsync(a => a.Reason == "DEMO_ADJUSTMENT_PENDING", cancellationToken))
        {
            var draft = await _adjustmentWorkflow.CreateDraftAsync(
                new CreateInventoryAdjustmentDraftCommand(
                    ioUserId,
                    "DEMO_ADJUSTMENT_PENDING",
                    new[]
                    {
                        new InventoryAdjustmentLineInput(inventory["Engine Oil"].Id, 3m, "Found stock")
                    }),
                cancellationToken);

            await _adjustmentWorkflow.SubmitAsync(draft.AdjustmentId, ioUserId, cancellationToken);
        }
    }
}
