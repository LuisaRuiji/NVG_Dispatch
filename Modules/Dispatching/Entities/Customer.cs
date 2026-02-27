namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Trip> Trips { get; set; } = [];
}
