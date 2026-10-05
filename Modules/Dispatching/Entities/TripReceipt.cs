using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

/// <summary>
/// Immutable financial snapshot of a completed trip. Corrections are represented
/// by linked reversal records; this record is never edited after generation.
/// </summary>
public sealed class TripReceipt
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "PHP";
    public string? ContainerNumber { get; set; }
    public string? BookingNumber { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public decimal BaseCharge { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public bool IsReversal { get; set; }
    public Guid? ReversesReceiptId { get; set; }
    public TripReceipt? ReversesReceipt { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public User? GeneratedByUser { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<TripReceiptCharge> AdditionalCharges { get; set; } = [];
}

/// <summary>One immutable additional-charge line belonging to a trip receipt.</summary>
public sealed class TripReceiptCharge
{
    public Guid Id { get; set; }
    public Guid TripReceiptId { get; set; }
    public TripReceipt? TripReceipt { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
