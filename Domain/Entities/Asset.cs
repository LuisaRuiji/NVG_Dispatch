using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Entities;

public sealed class Asset
{
    public Guid Id { get; set; }
    public AssetType AssetType { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string? PlateNo { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Active;
    public DateTime CreatedAt { get; set; }

    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
