using HoneyDrunk.Audit.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PocketQuests.Data.Configurations.Audit;

/// <summary>Preserves the shared Audit table mapping without changing its ownership.</summary>
public sealed class AuditRecordMapping : IEntityTypeConfiguration<AuditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords", "dbo");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasMaxLength(32).IsUnicode(false);
        builder.Property(row => row.EventName).HasMaxLength(200);
        builder.Property(row => row.TenantId).HasMaxLength(100);
        builder.HasIndex(row => new { row.TenantId, row.OccurredAt });
    }
}
