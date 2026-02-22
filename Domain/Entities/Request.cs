using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class Request
{
    public Guid Id { get; set; }
    public RequestType RequestType { get; set; }
    public Guid RequesterUserId { get; set; }
    public Guid? AssetId { get; set; }
    public string? Purpose { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? Requester { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<RequestLine> Lines { get; set; } = new List<RequestLine>();

    public Loan? Loan { get; set; }
}
