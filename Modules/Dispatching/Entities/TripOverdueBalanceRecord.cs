using NVGInventory.Domain.Entities;

namespace NVGInventory.Modules.Dispatching.Entities;

/// <summary>One-time Finance record for a delivered trip's unpaid final balance.</summary>
public sealed class TripOverdueBalanceRecord
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
    public DateTime RecordedAt { get; set; }
}
