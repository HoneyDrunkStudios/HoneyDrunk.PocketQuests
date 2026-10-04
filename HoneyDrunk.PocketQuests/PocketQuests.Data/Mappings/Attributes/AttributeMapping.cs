using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Attributes;

namespace PocketQuests.Data.Mappings.Attributes;

/// <summary>Maps Attribute fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AttributeMapping : IEntityTypeConfiguration<AttributeEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AttributeEntity> entity)
    {
        entity.ToTable("Attribute", "pocketquests", table =>
        {
            table.HasComment("One row is one canonical attribute. Classification: Public. History: reference; Retain stable codes indefinitely; source control owns reference history. Not erased with accounts.");
            table.HasCheckConstraint("CK_Attribute_SortOrder", "[SortOrder] >= 1");
            table.HasCheckConstraint("CK_Attribute_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_Attribute_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_Attribute_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_Attribute").IsClustered();
        entity.HasIndex(row => row.SortOrder, "UQ_Attribute_SortOrder").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("varchar(40)").HasComment("Stable public catalog code; preserve existing IDs and ordinal allocation tie-breaking.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Name).HasColumnType("nvarchar(120)").HasComment("Current public catalog display name.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CatalogVersion).HasColumnType("varchar(32)").HasComment("Version of the reviewed catalog that supplied this row.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.SortOrder).HasColumnType("int").HasComment("Positive canonical display order; not an independently chosen progression priority.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
    }
}
