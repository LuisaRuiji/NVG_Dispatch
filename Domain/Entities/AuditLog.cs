namespace NVGInventory.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? Actor { get; set; }
}
