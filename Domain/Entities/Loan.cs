using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class Loan
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid BorrowerUserId { get; set; }
    public Guid? AssetId { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Open;
    public DateTime IssuedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public Request? Request { get; set; }
    public User? Borrower { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<LoanLine> Lines { get; set; } = new List<LoanLine>();
}
