namespace NVGInventory.Domain.Entities;

public sealed class MfaChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Method { get; set; } = "TOTP";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
}
