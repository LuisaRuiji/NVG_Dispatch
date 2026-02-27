namespace NVGInventory.Domain.Entities;

public sealed class AuthEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Username { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string? RolesSnapshotJson { get; set; }
    public string AuthMethod { get; set; } = string.Empty;
    public bool MfaPerformed { get; set; }
    public string? MfaMethod { get; set; }
    public string? SessionId { get; set; }
    public string? TokenJti { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientApp { get; set; }
    public string? Environment { get; set; }
    public DateTime CreatedAt { get; set; }
}
