using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities;

namespace PocketQuests.Data.Configurations;

/// <summary>Fluent mapping for the LifecycleBarriers table.</summary>
public sealed class LifecycleBarrierEntityConfiguration : IEntityTypeConfiguration<LifecycleBarrierEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LifecycleBarrierEntity> builder)
    {
        builder.ToTable("LifecycleBarriers", "dbo");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasMaxLength(30).IsUnicode(false);
    }
}
