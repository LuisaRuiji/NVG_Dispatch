namespace NVGInventory.Domain.Entities;

public sealed class RequestLine
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid InventoryId { get; set; }
    public decimal QtyRequested { get; set; }
    public decimal? QtyApproved { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }

    public Request? Request { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
