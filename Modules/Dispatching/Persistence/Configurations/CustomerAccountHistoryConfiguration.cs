using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class CustomerAccountHistoryConfiguration : IEntityTypeConfiguration<CustomerAccountHistory>
{
    public void Configure(EntityTypeBuilder<CustomerAccountHistory> entity)
    {
        entity.ToTable("dispatch_customer_account_history");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.CustomerId).HasColumnName("customer_id");
        entity.Property(item => item.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.FromCreditStatus).HasColumnName("from_credit_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ToCreditStatus).HasColumnName("to_credit_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(item => item.ActorRole).HasColumnName("actor_role").HasMaxLength(200).IsRequired();
        entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        entity.Property(item => item.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(item => item.Customer).WithMany(customer => customer.AccountHistory).HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.CustomerId, item.ChangedAt });
    }
}
