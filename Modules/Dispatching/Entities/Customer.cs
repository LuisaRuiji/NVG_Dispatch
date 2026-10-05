using NVGInventory.Domain.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public CustomerAccountStatus AccountStatus { get; set; } = CustomerAccountStatus.ActivePrepaid;
    public DateTime AccountRequestedAt { get; set; }
    public Guid? AccountReviewedByUserId { get; set; }
    public User? AccountReviewedByUser { get; set; }
    public DateTime? AccountReviewedAt { get; set; }
    public string? AccountStatusReason { get; set; }
    public CustomerCreditStatus CreditStatus { get; set; } = CustomerCreditStatus.NotGranted;
    public decimal? CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool HasOverdueBalance { get; set; }
    public Guid? CreditReviewedByUserId { get; set; }
    public User? CreditReviewedByUser { get; set; }
    public DateTime? CreditReviewedAt { get; set; }
    public string? CreditTermsReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<Trip> Trips { get; set; } = [];
    public List<CustomerAccountHistory> AccountHistory { get; set; } = [];
}
