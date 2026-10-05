using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record TripReceiptChargeCommand(string Description, decimal Amount);

public sealed record GenerateTripReceiptCommand(
    string ReceiptNumber,
    decimal? BaseCharge,
    IReadOnlyCollection<TripReceiptChargeCommand>? AdditionalCharges,
    decimal? DiscountAmount,
    decimal? DiscountPercent,
    string? DiscountReason,
    decimal? TaxAmount,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record TripReceiptChargeResult(string Description, decimal Amount);

public sealed record TripReceiptResult(
    Guid TripId,
    string ReceiptNumber,
    DateTime GeneratedAt,
    string Currency,
    string CustomerName,
    string? ContainerNumber,
    string? BookingNumber,
    string? PickupLocation,
    string? DropoffLocation,
    decimal BaseCharge,
    IReadOnlyCollection<TripReceiptChargeResult> AdditionalCharges,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal? DiscountPercent,
    string? DiscountReason,
    decimal TaxAmount,
    decimal Total,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed class TripReceiptService
{
    private static readonly TripStatus[] ReceiptEligibleStatuses =
    [
        TripStatus.DeliveryCompleted,
        TripStatus.DocumentsPending,
        TripStatus.OperationallyClosed,
        // Accepted while older records are upgraded to the controlled lifecycle.
        TripStatus.Delivered,
        TripStatus.Closed
    ];

    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TripReceiptService(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<TripReceiptResult> GenerateAsync(
        Guid tripId,
        GenerateTripReceiptCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(actor);
        var receiptNumber = NormalizeRequired(command.ReceiptNumber, "Receipt number", 80);
        var trip = await _dbContext.DispatchTrips
            .Include(item => item.Customer)
            .Include(item => item.Stops)
            .FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        if (!ReceiptEligibleStatuses.Contains(trip.Status))
            throw new ConflictDomainException("A receipt can be generated only after delivery is completed.");

        var baseCharge = Money(command.BaseCharge ?? trip.Rate ?? 0m);
        if (baseCharge <= 0)
            throw new BusinessRuleViolationException("Enter a positive base charge or set the trip rate before generating a receipt.");

        var charges = NormalizeCharges(command.AdditionalCharges);
        var subtotal = Money(baseCharge + charges.Sum(item => item.Amount));
        var discount = CalculateDiscount(subtotal, command.DiscountAmount, command.DiscountPercent);
        var taxAmount = Money(command.TaxAmount ?? 0m);
        if (taxAmount < 0)
            throw new BusinessRuleViolationException("Tax amount cannot be negative.");

        var total = Money(subtotal - discount + taxAmount);
        if (total < 0)
            throw new BusinessRuleViolationException("Discount cannot exceed the receipt subtotal plus tax.");

        trip.OfficialReceiptNumber = receiptNumber;
        trip.UpdatedAt = DateTime.UtcNow;
        _auditService.AddEntry(
            actor.UserId,
            AuditActions.TripReceiptGenerated,
            EntityTypes.TripReceipt,
            trip.Id,
            actorRole: ResolveActorRole(actor),
            tripId: trip.Id,
            reason: "Trip charge receipt generated.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        var pickup = trip.Stops.FirstOrDefault(item => item.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(item => item.StopType == TripStopType.Dropoff);
        return new TripReceiptResult(
            trip.Id,
            receiptNumber,
            DateTime.UtcNow,
            "PHP",
            trip.Customer?.Name ?? "Unknown customer",
            trip.ContainerNumber,
            trip.BookingNumber,
            pickup?.LocationText,
            dropoff?.LocationText,
            baseCharge,
            charges,
            subtotal,
            discount,
            command.DiscountPercent is > 0 ? Money(command.DiscountPercent.Value) : null,
            NormalizeOptional(command.DiscountReason, 300),
            taxAmount,
            total,
            NormalizeOptional(command.PaymentMethod, 80),
            NormalizeOptional(command.PaymentReference, 120),
            NormalizeOptional(command.Notes, 600));
    }

    private static IReadOnlyCollection<TripReceiptChargeResult> NormalizeCharges(
        IReadOnlyCollection<TripReceiptChargeCommand>? charges)
    {
        if (charges is null || charges.Count == 0) return [];
        if (charges.Count > 20)
            throw new BusinessRuleViolationException("A receipt can contain at most 20 additional charge lines.");

        return charges.Select(item =>
        {
            var description = NormalizeRequired(item.Description, "Charge description", 120);
            var amount = Money(item.Amount);
            if (amount <= 0)
                throw new BusinessRuleViolationException($"Charge '{description}' must have a positive amount.");
            return new TripReceiptChargeResult(description, amount);
        }).ToList();
    }

    private static decimal CalculateDiscount(decimal subtotal, decimal? discountAmount, decimal? discountPercent)
    {
        var amount = Money(discountAmount ?? 0m);
        var percent = Money(discountPercent ?? 0m);
        if (amount < 0 || percent < 0)
            throw new BusinessRuleViolationException("Discount cannot be negative.");
        if (amount > 0 && percent > 0)
            throw new BusinessRuleViolationException("Use either a fixed discount or a percentage discount, not both.");
        if (percent > 100)
            throw new BusinessRuleViolationException("Discount percentage cannot exceed 100%.");

        var discount = percent > 0 ? Money(subtotal * percent / 100m) : amount;
        if (discount > subtotal)
            throw new BusinessRuleViolationException("Discount cannot exceed the receipt subtotal.");
        return discount;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeRequired(string? value, string label, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new BusinessRuleViolationException($"{label} is required.");
        if (normalized.Length > maxLength)
            throw new BusinessRuleViolationException($"{label} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength)
            throw new BusinessRuleViolationException($"Value cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static void EnsureAuthorized(DispatchActorContext actor)
    {
        if (!actor.IsFinance && !actor.IsAdmin)
            throw new ForbiddenDomainException("Only Finance or an administrator can generate trip receipts.");
    }

    private static string ResolveActorRole(DispatchActorContext actor) =>
        actor.IsFinance ? RoleNames.HeadOfFinance : RoleNames.Admin;
}
