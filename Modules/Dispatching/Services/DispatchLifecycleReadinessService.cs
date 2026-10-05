using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchReadinessResult(bool IsReady, IReadOnlyCollection<string> Blockers);

public sealed class DispatchLifecycleReadinessService
{
    private readonly InventoryDbContext _dbContext;

    public DispatchLifecycleReadinessService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DispatchReadinessResult> EvaluatePreDispatchAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stops)
            .Include(item => item.Documents)
            .FirstAsync(item => item.Id == tripId, cancellationToken);
        var booking = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.ConvertedTripId == tripId, cancellationToken);
        var blockers = new List<string>();
        if (trip.Customer is null || trip.Customer.AccountStatus is not (CustomerAccountStatus.ActivePrepaid or CustomerAccountStatus.ActiveCredit))
            blockers.Add("The customer account must be active before dispatch release.");
        if (trip.Customer?.HasOverdueBalance == true && booking?.FinanceClearanceStatus != BookingFinanceClearanceStatus.AuthorizedException)
            blockers.Add("The customer has an overdue balance and no authorized exception.");
        if (booking is null || booking.Status != ShipmentRequestStatus.ConvertedToTrip ||
            booking.FinanceClearanceStatus is not (BookingFinanceClearanceStatus.Cleared or BookingFinanceClearanceStatus.AuthorizedException))
            blockers.Add("The linked booking must be approved and cleared by Finance.");
        if (!trip.DriverUserId.HasValue) blockers.Add("Assign a driver.");
        if (!trip.TruckAssetId.HasValue) blockers.Add("Assign a truck.");
        if (!trip.TrailerAssetId.HasValue) blockers.Add("Assign a trailer.");
        if (string.IsNullOrWhiteSpace(trip.ContainerNumber)) blockers.Add("Set the container number.");
        var pickup = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff);
        if (string.IsNullOrWhiteSpace(pickup?.LocationText) || string.IsNullOrWhiteSpace(dropoff?.LocationText)) blockers.Add("Set valid pickup and dropoff stops.");
        if (pickup?.ScheduledAt is not DateTime pickupAt || dropoff?.ScheduledAt is not DateTime dropoffAt || pickupAt >= dropoffAt)
            blockers.Add("Set a pickup time earlier than the dropoff time.");
        blockers.AddRange(await EvaluateDocumentRulesAsync(trip, booking, DispatchDocumentMilestone.PreDispatch, cancellationToken));
        return new DispatchReadinessResult(blockers.Count == 0, blockers.Distinct().ToList());
    }

    public async Task<DispatchReadinessResult> EvaluateOperationalCloseAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(item => item.Documents)
            .Include(item => item.ContainerInspection)
            .FirstAsync(item => item.Id == tripId, cancellationToken);
        var booking = await _dbContext.ShipmentRequests.AsNoTracking().Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.ConvertedTripId == tripId, cancellationToken);
        var blockers = await EvaluateDocumentRulesAsync(trip, booking, DispatchDocumentMilestone.OperationalClose, cancellationToken);
        return new DispatchReadinessResult(blockers.Count == 0, blockers.Distinct().ToList());
    }

    private async Task<List<string>> EvaluateDocumentRulesAsync(
        Trip trip,
        ShipmentRequest? booking,
        DispatchDocumentMilestone milestone,
        CancellationToken cancellationToken)
    {
        var normalized = DispatchDocumentRules.NormalizeTripType(trip.TripType);
        var configured = await _dbContext.DispatchDocumentRules.AsNoTracking()
            .Where(rule => rule.IsActive && rule.IsRequired && rule.TripType == normalized && rule.Milestone == milestone)
            .Select(rule => new DispatchRequiredDocument(rule.DocumentCode, rule.Scope, rule.Direction, rule.AlternativeGroup, rule.Notes))
            .ToListAsync(cancellationToken);
        var rules = configured.Count > 0 ? configured : DispatchDocumentRules.GetDefaults(normalized, milestone).ToList();
        var blockers = new List<string>();

        foreach (var rule in rules.Where(rule => string.IsNullOrWhiteSpace(rule.AlternativeGroup)))
        {
            if (!IsSatisfied(rule, trip, booking)) blockers.Add(BuildMissingMessage(rule));
        }
        foreach (var group in rules.Where(rule => !string.IsNullOrWhiteSpace(rule.AlternativeGroup)).GroupBy(rule => rule.AlternativeGroup!))
        {
            if (!group.Any(rule => IsSatisfied(rule, trip, booking)))
                blockers.Add($"Provide and verify one of: {string.Join(", ", group.Select(rule => Label(rule.DocumentCode)))}.");
        }
        return blockers;
    }

    private static bool IsSatisfied(DispatchRequiredDocument rule, Trip trip, ShipmentRequest? booking)
    {
        if (rule.Scope == "BOOKING")
            return rule.DocumentCode == "BOOKING_REFERENCE" && !string.IsNullOrWhiteSpace(booking?.BookingNumber ?? trip.BookingNumber);
        if (rule.Scope == "INSPECTION")
            return trip.ContainerInspection?.Outcome is ContainerInspectionOutcome.Pass or ContainerInspectionOutcome.ClientApproval;

        if (rule.Scope is "SHIPMENT" or "SHIPMENT_OR_TRIP")
        {
            if (booking?.Documents.Any(document =>
                    document.VerificationState == TripDocumentState.Verified &&
                    string.Equals(ToCode(document.DocumentType), rule.DocumentCode, StringComparison.OrdinalIgnoreCase) &&
                    (!document.ExpiryDate.HasValue || document.ExpiryDate.Value >= DateTime.UtcNow)) == true)
                return true;
            if (rule.Scope == "SHIPMENT") return false;
        }

        if (!Enum.TryParse<TripDocumentType>(rule.DocumentCode.Replace("_", string.Empty), true, out var type)) return false;
        return trip.Documents.Any(document =>
            document.IsActive &&
            document.State == TripDocumentState.Verified &&
            (document.Type == type || (rule.DocumentCode == "POD" && document.Type == TripDocumentType.Dr && document.IsProofOfDelivery)) &&
            (rule.Direction == DocumentDirection.NotApplicable || document.Direction == rule.Direction) &&
            (!document.ExpiryDate.HasValue || document.ExpiryDate.Value >= DateTime.UtcNow));
    }

    private static string ToCode(ShipmentRequestDocumentType type) => type switch
    {
        ShipmentRequestDocumentType.Atw => "ATW",
        ShipmentRequestDocumentType.BookingConfirmation => "BOOKING_CONFIRMATION",
        ShipmentRequestDocumentType.ReleaseConfirmation => "RELEASE_CONFIRMATION",
        ShipmentRequestDocumentType.TerminalAuthorization => "TERMINAL_AUTHORIZATION",
        ShipmentRequestDocumentType.DeliveryOrder => "DELIVERY_ORDER",
        ShipmentRequestDocumentType.Cro => "CRO",
        ShipmentRequestDocumentType.WebCro => "WEB_CRO",
        ShipmentRequestDocumentType.ReturnDepotAuthorization => "RETURN_DEPOT_AUTHORIZATION",
        ShipmentRequestDocumentType.ReturnInstruction => "RETURN_INSTRUCTION",
        _ => type.ToString().ToUpperInvariant()
    };

    private static string BuildMissingMessage(DispatchRequiredDocument rule)
    {
        var direction = rule.Direction == DocumentDirection.NotApplicable ? string.Empty : $" ({rule.Direction})";
        return $"{Label(rule.DocumentCode)}{direction} must be provided and verified.";
    }

    private static string Label(string code) => code.Replace('_', ' ');
}
