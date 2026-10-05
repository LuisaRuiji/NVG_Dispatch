using NVGInventory.Domain.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Entities;

public sealed class BookingFinanceHistory
{
    public Guid Id { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public ShipmentRequest ShipmentRequest { get; set; } = null!;
    public BookingFinanceClearanceStatus FromStatus { get; set; }
    public BookingFinanceClearanceStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public User Actor { get; set; } = null!;
    public string ActorRole { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
