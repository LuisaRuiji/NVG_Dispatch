using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record TripReceiptChargeCommand(string Description, decimal Amount);
public sealed record GenerateTripReceiptCommand(string ReceiptNumber, decimal? BaseCharge, IReadOnlyCollection<TripReceiptChargeCommand>? AdditionalCharges, decimal? DiscountAmount, decimal? DiscountPercent, string? DiscountReason, decimal? TaxAmount, string? PaymentMethod, string? PaymentReference, string? Notes);
public sealed record TripReceiptChargeResult(string Description, decimal Amount);
public sealed record TripReceiptResult(Guid Id, Guid TripId, string ReceiptNumber, DateTime GeneratedAt, string Currency, string CustomerName, string? ContainerNumber, string? BookingNumber, string? PickupLocation, string? DropoffLocation, decimal BaseCharge, IReadOnlyCollection<TripReceiptChargeResult> AdditionalCharges, decimal Subtotal, decimal DiscountAmount, decimal? DiscountPercent, string? DiscountReason, decimal TaxAmount, decimal Total, string? PaymentMethod, string? PaymentReference, string? Notes, bool IsReversal, Guid? ReversesReceiptId, string? CorrectionReason, Guid GeneratedByUserId);

/// <summary>Generates and retrieves immutable trip-charge receipts and their reversal records.</summary>
public sealed class TripReceiptService
{
    private static readonly TripStatus[] ReceiptEligibleStatuses = [TripStatus.DeliveryCompleted, TripStatus.DocumentsPending, TripStatus.OperationallyClosed, TripStatus.Delivered, TripStatus.Closed];
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TripReceiptService(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    /// <summary>Creates an immutable receipt snapshot after delivery, or a replacement after reversal.</summary>
    public async Task<TripReceiptResult> GenerateAsync(Guid tripId, GenerateTripReceiptCommand command, DispatchActorContext actor, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(actor);
        var receiptNumber = NormalizeRequired(command.ReceiptNumber, "Receipt number", 80);
        var trip = await _dbContext.DispatchTrips.Include(item => item.Customer).Include(item => item.Stops)
            .FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken) ?? throw new NotFoundException("Trip not found.");
        if (!ReceiptEligibleStatuses.Contains(trip.Status)) throw new ConflictDomainException("A receipt can be generated only after delivery is completed.");
        if (await _dbContext.TripReceipts.AnyAsync(item => item.ReceiptNumber == receiptNumber, cancellationToken)) throw new ConflictDomainException("Receipt number is already in use.");

        var originalIds = await _dbContext.TripReceipts.Where(item => item.TripId == tripId && !item.IsReversal).Select(item => item.Id).ToListAsync(cancellationToken);
        var reversedIds = await _dbContext.TripReceipts.Where(item => item.IsReversal && item.ReversesReceiptId.HasValue && originalIds.Contains(item.ReversesReceiptId.Value)).Select(item => item.ReversesReceiptId!.Value).ToListAsync(cancellationToken);
        if (originalIds.Any(id => !reversedIds.Contains(id))) throw new ConflictDomainException("The active receipt is immutable. Reverse it with a reason before issuing a corrected receipt.");

        var baseCharge = Money(command.BaseCharge ?? trip.Rate ?? 0m);
        if (baseCharge <= 0) throw new BusinessRuleViolationException("Enter a positive base charge or set the trip rate before generating a receipt.");
        var charges = NormalizeCharges(command.AdditionalCharges);
        var subtotal = Money(baseCharge + charges.Sum(item => item.Amount));
        var discount = CalculateDiscount(subtotal, command.DiscountAmount, command.DiscountPercent);
        var taxAmount = Money(command.TaxAmount ?? 0m);
        if (taxAmount < 0) throw new BusinessRuleViolationException("Tax amount cannot be negative.");
        var total = Money(subtotal - discount + taxAmount);
        if (total < 0) throw new BusinessRuleViolationException("Discount cannot exceed the receipt subtotal plus tax.");

        var pickup = trip.Stops.FirstOrDefault(item => item.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(item => item.StopType == TripStopType.Dropoff);
        var receipt = new TripReceipt
        {
            Id = Guid.NewGuid(), TripId = trip.Id, CustomerId = trip.CustomerId, CustomerName = trip.Customer?.Name ?? "Unknown customer", ReceiptNumber = receiptNumber, Currency = "PHP",
            ContainerNumber = trip.ContainerNumber, BookingNumber = trip.BookingNumber, PickupLocation = pickup?.LocationText, DropoffLocation = dropoff?.LocationText,
            BaseCharge = baseCharge, Subtotal = subtotal, DiscountAmount = discount, DiscountPercent = command.DiscountPercent is > 0 ? Money(command.DiscountPercent.Value) : null,
            DiscountReason = NormalizeOptional(command.DiscountReason, 300), TaxAmount = taxAmount, Total = total, PaymentMethod = NormalizeOptional(command.PaymentMethod, 80),
            PaymentReference = NormalizeOptional(command.PaymentReference, 120), Notes = NormalizeOptional(command.Notes, 600), GeneratedByUserId = actor.UserId, GeneratedAt = DateTime.UtcNow,
            AdditionalCharges = charges.Select(item => new TripReceiptCharge { Id = Guid.NewGuid(), Description = item.Description, Amount = item.Amount }).ToList()
        };
        _dbContext.TripReceipts.Add(receipt);
        trip.OfficialReceiptNumber = receiptNumber;
        trip.UpdatedAt = receipt.GeneratedAt;
        _auditService.AddEntry(actor.UserId, AuditActions.TripReceiptGenerated, EntityTypes.TripReceipt, receipt.Id, after: new { receipt.ReceiptNumber, receipt.Total, receipt.TripId }, actorRole: ResolveActorRole(actor), tripId: trip.Id, reason: "Immutable trip charge receipt generated.", referenceNumber: receiptNumber);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(receipt);
    }

    /// <summary>Returns the ordered receipt ledger shown in the trip's finance history.</summary>
    public async Task<IReadOnlyCollection<TripReceiptResult>> GetHistoryAsync(Guid tripId, DispatchActorContext actor, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(actor);
        if (!await _dbContext.DispatchTrips.AnyAsync(item => item.Id == tripId, cancellationToken)) throw new NotFoundException("Trip not found.");
        var receipts = await _dbContext.TripReceipts.AsNoTracking().Include(item => item.AdditionalCharges).Where(item => item.TripId == tripId).OrderByDescending(item => item.GeneratedAt).ToListAsync(cancellationToken);
        return receipts.Select(Map).ToList();
    }

    /// <summary>Creates a linked negative reversal; the original receipt is retained and cannot be edited.</summary>
    public async Task<TripReceiptResult> ReverseAsync(Guid tripId, Guid receiptId, string reason, DispatchActorContext actor, CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(actor);
        var normalizedReason = NormalizeRequired(reason, "Correction reason", 600);
        var receipt = await _dbContext.TripReceipts.Include(item => item.AdditionalCharges).FirstOrDefaultAsync(item => item.Id == receiptId && item.TripId == tripId, cancellationToken) ?? throw new NotFoundException("Receipt not found for this trip.");
        if (receipt.IsReversal) throw new ConflictDomainException("A reversal cannot be reversed.");
        if (await _dbContext.TripReceipts.AnyAsync(item => item.ReversesReceiptId == receiptId, cancellationToken)) throw new ConflictDomainException("This receipt has already been reversed.");
        var reversal = new TripReceipt
        {
            Id = Guid.NewGuid(), TripId = receipt.TripId, CustomerId = receipt.CustomerId, CustomerName = receipt.CustomerName, ReceiptNumber = $"RV-{Guid.NewGuid():N}"[..35], Currency = receipt.Currency,
            ContainerNumber = receipt.ContainerNumber, BookingNumber = receipt.BookingNumber, PickupLocation = receipt.PickupLocation, DropoffLocation = receipt.DropoffLocation,
            BaseCharge = -receipt.BaseCharge, Subtotal = -receipt.Subtotal, DiscountAmount = -receipt.DiscountAmount, DiscountPercent = receipt.DiscountPercent, DiscountReason = receipt.DiscountReason,
            TaxAmount = -receipt.TaxAmount, Total = -receipt.Total, PaymentMethod = "REVERSAL", PaymentReference = receipt.ReceiptNumber, Notes = receipt.Notes,
            IsReversal = true, ReversesReceiptId = receipt.Id, CorrectionReason = normalizedReason, GeneratedByUserId = actor.UserId, GeneratedAt = DateTime.UtcNow,
            AdditionalCharges = receipt.AdditionalCharges.Select(item => new TripReceiptCharge { Id = Guid.NewGuid(), Description = item.Description, Amount = -item.Amount }).ToList()
        };
        _dbContext.TripReceipts.Add(reversal);
        _auditService.AddEntry(actor.UserId, AuditActions.TripReceiptReversed, EntityTypes.TripReceipt, reversal.Id, before: new { receipt.ReceiptNumber, receipt.Total }, after: new { reversal.ReceiptNumber, reversal.Total, reversal.ReversesReceiptId }, actorRole: ResolveActorRole(actor), tripId: tripId, reason: normalizedReason, referenceNumber: receipt.ReceiptNumber);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(reversal);
    }

    private static TripReceiptResult Map(TripReceipt receipt) => new(receipt.Id, receipt.TripId, receipt.ReceiptNumber, receipt.GeneratedAt, receipt.Currency, receipt.CustomerName, receipt.ContainerNumber, receipt.BookingNumber, receipt.PickupLocation, receipt.DropoffLocation, receipt.BaseCharge, receipt.AdditionalCharges.Select(item => new TripReceiptChargeResult(item.Description, item.Amount)).ToList(), receipt.Subtotal, receipt.DiscountAmount, receipt.DiscountPercent, receipt.DiscountReason, receipt.TaxAmount, receipt.Total, receipt.PaymentMethod, receipt.PaymentReference, receipt.Notes, receipt.IsReversal, receipt.ReversesReceiptId, receipt.CorrectionReason, receipt.GeneratedByUserId);
    private static IReadOnlyCollection<TripReceiptChargeResult> NormalizeCharges(IReadOnlyCollection<TripReceiptChargeCommand>? charges)
    {
        if (charges is null || charges.Count == 0) return [];
        if (charges.Count > 20) throw new BusinessRuleViolationException("A receipt can contain at most 20 additional charge lines.");
        return charges.Select(item => { var description = NormalizeRequired(item.Description, "Charge description", 120); var amount = Money(item.Amount); if (amount <= 0) throw new BusinessRuleViolationException($"Charge '{description}' must have a positive amount."); return new TripReceiptChargeResult(description, amount); }).ToList();
    }
    private static decimal CalculateDiscount(decimal subtotal, decimal? discountAmount, decimal? discountPercent)
    {
        var amount = Money(discountAmount ?? 0m); var percent = Money(discountPercent ?? 0m);
        if (amount < 0 || percent < 0) throw new BusinessRuleViolationException("Discount cannot be negative.");
        if (amount > 0 && percent > 0) throw new BusinessRuleViolationException("Use either a fixed discount or a percentage discount, not both.");
        if (percent > 100) throw new BusinessRuleViolationException("Discount percentage cannot exceed 100%.");
        var discount = percent > 0 ? Money(subtotal * percent / 100m) : amount;
        if (discount > subtotal) throw new BusinessRuleViolationException("Discount cannot exceed the receipt subtotal.");
        return discount;
    }
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string NormalizeRequired(string? value, string label, int maxLength) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw new BusinessRuleViolationException($"{label} is required."); if (normalized.Length > maxLength) throw new BusinessRuleViolationException($"{label} cannot exceed {maxLength} characters."); return normalized; }
    private static string? NormalizeOptional(string? value, int maxLength) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) return null; if (normalized.Length > maxLength) throw new BusinessRuleViolationException("Value cannot exceed the allowed length."); return normalized; }
    private static void EnsureAuthorized(DispatchActorContext actor) { if (!actor.IsFinance && !actor.IsAdmin) throw new ForbiddenDomainException("Only Finance or an administrator can manage trip receipts."); }
    private static string ResolveActorRole(DispatchActorContext actor) => actor.IsFinance ? RoleNames.HeadOfFinance : RoleNames.Admin;
}
