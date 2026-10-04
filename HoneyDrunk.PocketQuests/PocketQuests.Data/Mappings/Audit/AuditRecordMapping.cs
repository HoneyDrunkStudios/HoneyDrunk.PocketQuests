using HoneyDrunk.Audit.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PocketQuests.Data.Mappings.Audit;

/// <summary>Preserves the shared Audit table mapping without changing its ownership.</summary>
public sealed class AuditRecordMapping : IEntityTypeConfiguration<AuditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecord> entity)
    {
        entity.ToTable("AuditRecords", "dbo");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32).IsUnicode(false);
        entity.Property(row => row.EventName).HasMaxLength(200);
        entity.Property(row => row.TenantId).HasMaxLength(100);
        entity.HasIndex(row => new { row.TenantId, row.OccurredAt });
    }
}
