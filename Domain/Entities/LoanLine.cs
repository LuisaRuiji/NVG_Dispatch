namespace NVGInventory.Domain.Entities;

public sealed class LoanLine
{
    public Guid Id { get; set; }
    public Guid LoanId { get; set; }
    public Guid InventoryId { get; set; }
    public decimal QtyIssued { get; set; }
    public decimal QtyReturned { get; set; }

    public Loan? Loan { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public ICollection<LoanLineReturn> Returns { get; set; } = new List<LoanLineReturn>();
}
