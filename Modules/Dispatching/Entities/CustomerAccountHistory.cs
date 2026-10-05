using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class CustomerAccountHistory
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public CustomerAccountStatus FromStatus { get; set; }
    public CustomerAccountStatus ToStatus { get; set; }
    public CustomerCreditStatus? FromCreditStatus { get; set; }
    public CustomerCreditStatus? ToCreditStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public User Actor { get; set; } = null!;
    public string ActorRole { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
