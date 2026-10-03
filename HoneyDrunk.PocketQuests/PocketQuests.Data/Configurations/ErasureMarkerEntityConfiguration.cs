using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities;

namespace PocketQuests.Data.Configurations;

/// <summary>Fluent mapping for the Erasures table.</summary>
public sealed class ErasureMarkerEntityConfiguration : IEntityTypeConfiguration<ErasureMarkerEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ErasureMarkerEntity> builder)
    {
        builder.ToTable("Erasures", "dbo");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasMaxLength(30).IsUnicode(false);
    }
}
