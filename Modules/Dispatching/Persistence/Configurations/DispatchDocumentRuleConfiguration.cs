using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Persistence.Configurations;

public sealed class DispatchDocumentRuleConfiguration : IEntityTypeConfiguration<DispatchDocumentRule>
{
    public void Configure(EntityTypeBuilder<DispatchDocumentRule> entity)
    {
        entity.ToTable("dispatch_document_rules");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.TripType).HasColumnName("trip_type").HasMaxLength(60).IsRequired();
        entity.Property(item => item.Milestone).HasColumnName("milestone").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.DocumentCode).HasColumnName("document_code").HasMaxLength(60).IsRequired();
        entity.Property(item => item.Scope).HasColumnName("scope").HasMaxLength(20).IsRequired();
        entity.Property(item => item.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(20);
        entity.Property(item => item.AlternativeGroup).HasColumnName("alternative_group").HasMaxLength(80);
        entity.Property(item => item.IsRequired).HasColumnName("is_required").HasDefaultValue(true);
        entity.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        entity.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(1000);
        entity.Property(item => item.UpdatedByUserId).HasColumnName("updated_by_user_id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        entity.HasOne(item => item.UpdatedByUser).WithMany().HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.TripType, item.Milestone, item.IsActive });
    }
}
