using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record PlanningApprovedBooking(
    Guid RequestId,
    Guid CustomerId,
    string CustomerName,
    string PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? RequestedPickupTime,
    string? ContainerSize,
    string? TripType,
    string? ContainerNumber,
    string? ShippingLine,
    string? BookingNumber,
    bool HasAtw,
    DateTime CreatedAt);

public sealed record PlanningAssignmentConflict(
    Guid TripId,
    string TripReference,
    DateTime WindowStart,
    DateTime WindowEnd,
    IReadOnlyCollection<string> Resources);

public sealed record PlanningTripCard(
    Guid TripId,
    TripStatus Status,
    Guid CustomerId,
    string CustomerName,
    string? BookingNumber,
    string? ContainerNumber,
    string? ContainerSize,
    string? TripType,
    string? PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    DateTime? PickupScheduledAt,
    string? DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? DropoffScheduledAt,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    Guid? TrailerAssetId,
    string? TrailerAssetCode,
    TripDocumentState AtwState,
    bool IsReadyForDispatch,
    IReadOnlyCollection<string> MissingRequirements,
    IReadOnlyCollection<PlanningAssignmentConflict> Conflicts,
    string RowVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PlanningBoardSnapshot(
    IReadOnlyCollection<PlanningApprovedBooking> ApprovedBookings,
    int ApprovedTotalCount,
    IReadOnlyCollection<PlanningTripCard> DraftTrips,
    int DraftTotalCount,
    IReadOnlyCollection<PlanningTripCard> ReadyTrips,
    int ReadyCount,
    int ConflictCount,
    int Page,
    int PageSize);

public sealed record PlanningResourceOption(
    Guid Id,
    string Code,
    string Label,
    string? Capability,
    string OperationalStatus,
    bool IsAvailable,
    Guid? ConflictTripId,
    string? ConflictTripReference,
    DateTime? BusyUntil);

public sealed record PlanningResourceSnapshot(
    IReadOnlyCollection<PlanningResourceOption> Drivers,
    IReadOnlyCollection<PlanningResourceOption> Trucks,
    IReadOnlyCollection<PlanningResourceOption> Trailers);

public sealed class DispatchPlanningService
{
    private readonly InventoryDbContext _dbContext;

    public DispatchPlanningService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanningBoardSnapshot> GetBoardAsync(
        int page,
        int pageSize,
        string? search,
        DateOnly? day,
        CancellationToken cancellationToken = default)
    {
        var requestQuery = _dbContext.ShipmentRequests
            .AsNoTracking()
            .Where(request => request.Status == ShipmentRequestStatus.Approved);
        var tripQuery = _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Draft || trip.Status == TripStatus.ReadyForDispatch);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            requestQuery = requestQuery.Where(request =>
                (request.Customer != null && request.Customer.Name.Contains(term)) ||
                request.PickupLocation.Contains(term) ||
                request.DropoffLocation.Contains(term) ||
                (request.BookingNumber != null && request.BookingNumber.Contains(term)) ||
                (request.ContainerNumber != null && request.ContainerNumber.Contains(term)));
            tripQuery = tripQuery.Where(trip =>
                (trip.Customer != null && trip.Customer.Name.Contains(term)) ||
                (trip.BookingNumber != null && trip.BookingNumber.Contains(term)) ||
                (trip.ContainerNumber != null && trip.ContainerNumber.Contains(term)) ||
                trip.Stops.Any(stop => stop.LocationText.Contains(term)));
        }

        if (day.HasValue)
        {
            var start = day.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            requestQuery = requestQuery.Where(request =>
                !request.RequestedPickupTime.HasValue ||
                (request.RequestedPickupTime >= start && request.RequestedPickupTime < end));
            tripQuery = tripQuery.Where(trip => trip.Stops.Any(stop =>
                stop.StopType == TripStopType.Pickup &&
                (!stop.ScheduledAt.HasValue || (stop.ScheduledAt >= start && stop.ScheduledAt < end))));
        }

        var approvedTotal = await requestQuery.CountAsync(cancellationToken);
        var draftQuery = tripQuery.Where(trip => trip.Status == TripStatus.Draft);
        var readyQuery = tripQuery.Where(trip => trip.Status == TripStatus.ReadyForDispatch);
        var draftTotal = await draftQuery.CountAsync(cancellationToken);
        var readyTotal = await readyQuery.CountAsync(cancellationToken);

        var approvedBookings = await requestQuery
            .OrderBy(request => !request.RequestedPickupTime.HasValue)
            .ThenBy(request => request.RequestedPickupTime)
            .ThenBy(request => request.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new PlanningApprovedBooking(
                request.Id,
                request.CustomerId,
                request.Customer != null ? request.Customer.Name : string.Empty,
                request.PickupLocation,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DropoffLocation,
                request.DropoffLatitude,
                request.DropoffLongitude,
                request.RequestedPickupTime,
                request.ContainerSize,
                request.TripType,
                request.ContainerNumber,
                request.ShippingLine,
                request.BookingNumber,
                request.Documents.Any(document => document.DocumentType == ShipmentRequestDocumentType.Atw),
                request.CreatedAt))
            .ToListAsync(cancellationToken);

        var draftTrips = await LoadTripCardsAsync(draftQuery, page, pageSize, cancellationToken);
        var readyTrips = await LoadTripCardsAsync(readyQuery, page, pageSize, cancellationToken);

        var conflictCandidates = await LoadConflictWindowsAsync(cancellationToken);
        var cards = draftTrips.Select(trip => BuildTripCard(trip, conflictCandidates)).ToList();
        var readyCards = readyTrips.Select(trip => BuildTripCard(trip, conflictCandidates)).ToList();

        return new PlanningBoardSnapshot(
            approvedBookings,
            approvedTotal,
            cards,
            draftTotal,
            readyCards,
            readyTotal,
            cards.Count(card => card.Conflicts.Count > 0),
            page,
            pageSize);
    }

    private async Task<List<Entities.Trip>> LoadTripCardsAsync(
        IQueryable<Entities.Trip> tripQuery,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await tripQuery
            .Include(trip => trip.Customer)
            .Include(trip => trip.Driver)
            .Include(trip => trip.TruckAsset)
            .Include(trip => trip.TrailerAsset)
            .Include(trip => trip.Stops)
            .Include(trip => trip.Documents)
            .OrderBy(trip => trip.Stops
                .Where(stop => stop.StopType == TripStopType.Pickup)
                .Select(stop => stop.ScheduledAt)
                .FirstOrDefault() == null)
            .ThenBy(trip => trip.Stops
                .Where(stop => stop.StopType == TripStopType.Pickup)
                .Select(stop => stop.ScheduledAt)
                .FirstOrDefault())
            .ThenBy(trip => trip.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningResourceSnapshot> GetResourcesAsync(
        DateTime? pickupAt,
        DateTime? dropoffAt,
        Guid? excludeTripId,
        CancellationToken cancellationToken = default)
    {
        if (pickupAt.HasValue != dropoffAt.HasValue)
        {
            throw new ArgumentException("Pickup and dropoff times must be provided together.");
        }
        if (pickupAt.HasValue && pickupAt.Value >= dropoffAt!.Value)
        {
            throw new ArgumentException("Pickup time must be earlier than dropoff time.");
        }

        var conflicts = pickupAt.HasValue
            ? (await LoadConflictWindowsAsync(cancellationToken))
                .Where(window => window.TripId != excludeTripId && window.Start < dropoffAt && window.End > pickupAt)
                .ToList()
            : [];

        var drivers = await _dbContext.DispatchDrivers
            .AsNoTracking()
            .Include(driver => driver.User)
            .OrderBy(driver => driver.User != null ? driver.User.Username : string.Empty)
            .ToListAsync(cancellationToken);
        var trucks = await _dbContext.DispatchTrucks
            .AsNoTracking()
            .Include(truck => truck.Asset)
            .OrderBy(truck => truck.PlateNumber)
            .ToListAsync(cancellationToken);
        var trailers = await _dbContext.DispatchTrailers
            .AsNoTracking()
            .Include(trailer => trailer.Asset)
            .OrderBy(trailer => trailer.TrailerCode)
            .ToListAsync(cancellationToken);

        return new PlanningResourceSnapshot(
            drivers.Where(driver => driver.User != null).Select(driver =>
            {
                var conflict = conflicts.FirstOrDefault(window => window.DriverUserId == driver.UserId);
                var active = driver.User!.IsActive && IsOperationallyActive(driver.Status);
                return ToResource(
                    driver.UserId,
                    driver.User.Username,
                    driver.User.Username,
                    driver.LicenseNumber,
                    driver.Status,
                    active,
                    conflict);
            }).ToList(),
            trucks.Where(truck => truck.Asset != null).Select(truck =>
            {
                var conflict = conflicts.FirstOrDefault(window => window.TruckAssetId == truck.AssetId);
                var active = truck.Asset!.Status == AssetStatus.Active && IsOperationallyActive(truck.Status);
                return ToResource(
                    truck.AssetId,
                    truck.Asset.AssetCode,
                    string.IsNullOrWhiteSpace(truck.PlateNumber) ? truck.Asset.AssetCode : truck.PlateNumber,
                    truck.ContainerCapability,
                    truck.Status,
                    active,
                    conflict);
            }).ToList(),
            trailers.Where(trailer => trailer.Asset != null).Select(trailer =>
            {
                var conflict = conflicts.FirstOrDefault(window => window.TrailerAssetId == trailer.AssetId);
                var active = trailer.Asset!.Status == AssetStatus.Active && IsOperationallyActive(trailer.Status);
                return ToResource(
                    trailer.AssetId,
                    trailer.TrailerCode,
                    trailer.TrailerCode,
                    trailer.ContainerType,
                    trailer.Status,
                    active,
                    conflict);
            }).ToList());
    }

    private async Task<List<PlanningWindow>> LoadConflictWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.Status != TripStatus.Cancelled && trip.Status != TripStatus.Closed)
            .Select(trip => new PlanningWindow(
                trip.Id,
                trip.DriverUserId,
                trip.TruckAssetId,
                trip.TrailerAssetId,
                trip.Stops.Where(stop => stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault(),
                trip.Stops.Where(stop => stop.StopType == TripStopType.Dropoff)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        // EF Core cannot translate nullable checks over a projected record constructor.
        // Keep the database projection narrow, then discard incomplete windows in memory.
        return windows
            .Where(window => window.Start.HasValue && window.End.HasValue)
            .ToList();
    }

    private static PlanningTripCard BuildTripCard(
        Entities.Trip trip,
        IReadOnlyCollection<PlanningWindow> candidates)
    {
        var pickup = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff);
        var atwState = trip.Documents
            .Where(document => document.IsActive && document.Type == TripDocumentType.Atw)
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => document.State)
            .FirstOrDefault();

        var conflicts = new List<PlanningAssignmentConflict>();
        if (pickup?.ScheduledAt is DateTime pickupAt && dropoff?.ScheduledAt is DateTime dropoffAt && pickupAt < dropoffAt)
        {
            conflicts = candidates
                .Where(window => window.TripId != trip.Id && window.Start < dropoffAt && window.End > pickupAt)
                .Select(window =>
                {
                    var resources = new List<string>();
                    if (trip.DriverUserId.HasValue && window.DriverUserId == trip.DriverUserId) resources.Add("Driver");
                    if (trip.TruckAssetId.HasValue && window.TruckAssetId == trip.TruckAssetId) resources.Add("Truck");
                    if (trip.TrailerAssetId.HasValue && window.TrailerAssetId == trip.TrailerAssetId) resources.Add("Trailer");
                    return new { Window = window, Resources = resources };
                })
                .Where(item => item.Resources.Count > 0)
                .Select(item => new PlanningAssignmentConflict(
                    item.Window.TripId,
                    BuildTripReference(item.Window.TripId),
                    item.Window.Start!.Value,
                    item.Window.End!.Value,
                    item.Resources))
                .ToList();
        }

        var missing = new List<string>();
        if (!trip.DriverUserId.HasValue) missing.Add("Assign a driver");
        if (!trip.TruckAssetId.HasValue) missing.Add("Assign a truck");
        if (string.IsNullOrWhiteSpace(trip.ContainerNumber)) missing.Add("Set the container number");
        if (string.IsNullOrWhiteSpace(pickup?.LocationText)) missing.Add("Set the pickup location");
        if (string.IsNullOrWhiteSpace(dropoff?.LocationText)) missing.Add("Set the dropoff location");
        if (!pickup?.ScheduledAt.HasValue ?? true) missing.Add("Set the pickup time");
        if (!dropoff?.ScheduledAt.HasValue ?? true) missing.Add("Set the dropoff time");
        if (pickup?.ScheduledAt.HasValue == true && dropoff?.ScheduledAt.HasValue == true && pickup.ScheduledAt >= dropoff.ScheduledAt)
            missing.Add("Make dropoff later than pickup");
        if (atwState != TripDocumentState.Verified) missing.Add("Verify the ATW");
        if (conflicts.Count > 0) missing.Add("Resolve assignment conflicts");

        return new PlanningTripCard(
            trip.Id,
            trip.Status,
            trip.CustomerId,
            trip.Customer?.Name ?? string.Empty,
            trip.BookingNumber,
            trip.ContainerNumber,
            trip.ContainerSize,
            trip.TripType,
            pickup?.LocationText,
            pickup?.Latitude,
            pickup?.Longitude,
            pickup?.ScheduledAt,
            dropoff?.LocationText,
            dropoff?.Latitude,
            dropoff?.Longitude,
            dropoff?.ScheduledAt,
            trip.DriverUserId,
            trip.Driver?.Username,
            trip.TruckAssetId,
            trip.TruckAsset?.AssetCode,
            trip.TrailerAssetId,
            trip.TrailerAsset?.AssetCode,
            atwState,
            missing.Count == 0,
            missing,
            conflicts,
            Convert.ToBase64String(trip.RowVersion),
            trip.CreatedAt,
            trip.UpdatedAt);
    }

    private static PlanningResourceOption ToResource(
        Guid id,
        string code,
        string label,
        string? capability,
        string status,
        bool active,
        PlanningWindow? conflict)
    {
        return new PlanningResourceOption(
            id,
            code,
            label,
            capability,
            status,
            active && conflict is null,
            conflict?.TripId,
            conflict is null ? null : BuildTripReference(conflict.TripId),
            conflict?.End);
    }

    private static bool IsOperationallyActive(string status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase);

    private static string BuildTripReference(Guid tripId) => tripId.ToString("N")[..8].ToUpperInvariant();

    private sealed record PlanningWindow(
        Guid TripId,
        Guid? DriverUserId,
        Guid? TruckAssetId,
        Guid? TrailerAssetId,
        DateTime? Start,
        DateTime? End);
}
