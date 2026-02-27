namespace NVGInventory.Domain.Entities;

public sealed class ModuleSetting
{
    public string ModuleKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
