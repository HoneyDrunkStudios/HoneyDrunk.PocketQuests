using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Configurations.Skills;

/// <summary>Maps Skill fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class SkillMapping : IEntityTypeConfiguration<SkillEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SkillEntity> entity)
    {
        entity.ToTable("Skill", "pocketquests", table =>
        {
            table.HasComment("One row is one canonical system skill. Classification: Public. History: reference; Retain stable codes indefinitely; source control owns reference history. Not erased with accounts.");
            table.HasCheckConstraint("CK_Skill_SortOrder", "[SortOrder] >= 1");
            table.HasCheckConstraint("CK_Skill_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_Skill_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_Skill_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_Skill").IsClustered();
        entity.HasIndex(row => row.SortOrder, "UQ_Skill_SortOrder").IsUnique().HasFilter(null);
        entity.HasIndex(row => row.NormalizedName, "UQ_Skill_NormalizedName").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasComment("Stable public catalog code; preserve existing IDs and ordinal allocation tie-breaking.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).ValueGeneratedNever();
        entity.Property(row => row.Name).HasComment("Current public catalog display name.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true);
        entity.Property(row => row.CatalogVersion).HasComment("Version of the reviewed catalog that supplied this row.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false);
        entity.Property(row => row.SortOrder).HasComment("Positive canonical display order; not an independently chosen progression priority.");
        entity.Property(row => row.NormalizedName).HasComment("Reserved system skill name normalized by the versioned OrdinalIgnoreCase-equivalent normalizer; compare with BIN2 collation.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(true);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
    }
}
