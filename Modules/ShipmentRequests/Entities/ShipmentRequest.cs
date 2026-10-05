using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Entities;

public sealed class ShipmentRequest
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ShipmentRequestStatus Status { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }
    public string DropoffLocation { get; set; } = string.Empty;
    public decimal? DropoffLatitude { get; set; }
    public decimal? DropoffLongitude { get; set; }
    public DateTime? RequestedPickupTime { get; set; }
    public string? CargoDescription { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? ContainerSize { get; set; }
    public string? TripType { get; set; }
    public string? ContainerNumber { get; set; }
    public string? ShippingLine { get; set; }
    public string? BookingNumber { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? RejectionRemarks { get; set; }
    public BookingFinanceClearanceStatus FinanceClearanceStatus { get; set; } = BookingFinanceClearanceStatus.Cleared;
    public decimal? QuotedAmount { get; set; }
    public decimal? RequiredDepositAmount { get; set; }
    public decimal VerifiedPaymentAmount { get; set; }
    public decimal VerifiedDepositAmount { get; set; }
    public Guid? FinanceClearedByUserId { get; set; }
    public User? FinanceClearedByUser { get; set; }
    public DateTime? FinanceClearedAt { get; set; }
    public string? FinanceClearanceReason { get; set; }
    public Guid? FinanceExceptionByUserId { get; set; }
    public User? FinanceExceptionByUser { get; set; }
    public DateTime? FinanceExceptionAt { get; set; }
    public string? FinanceExceptionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public Guid? ConvertedTripId { get; set; }
    public Trip? ConvertedTrip { get; set; }

    public List<ShipmentRequestDocument> Documents { get; set; } = [];
    public List<BookingFinanceHistory> FinanceHistory { get; set; } = [];
}
