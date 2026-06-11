using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public string? MfaSecretKey { get; set; }
    public string? PendingMfaSecretKey { get; set; }
    public DateTime? MfaEnabledAt { get; set; }
    public DateTime? MfaLastVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
