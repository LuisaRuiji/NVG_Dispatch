using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class LoanLineReturn
{
    public Guid Id { get; set; }
    public Guid LoanLineId { get; set; }
    public decimal QtyReturned { get; set; }
    public ReturnCondition Condition { get; set; }
    public string? MissingComponentsJson { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTime ReturnedAt { get; set; }

    public LoanLine? LoanLine { get; set; }
    public User? ReceivedBy { get; set; }
}
