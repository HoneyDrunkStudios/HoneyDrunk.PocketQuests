using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Mappings.Quests;

/// <summary>Maps SystemQuest fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class SystemQuestMapping : IEntityTypeConfiguration<SystemQuestEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SystemQuestEntity> entity)
    {
        entity.ToTable("SystemQuest", "pocketquests", table =>
        {
            table.HasComment("One row is one stable system quest identity whose full template is owned by the versioned catalog. Classification: Public. History: reference; Retain referenced codes; source-controlled template versions retain old terms. Not erased with accounts.");
            table.HasCheckConstraint("CK_SystemQuest_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_SystemQuest_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_SystemQuest_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_SystemQuest").IsClustered();
        entity.Property(row => row.Id).HasColumnType("varchar(40)").HasComment("Stable system quest code, for example PQ-CAT-Q01; never reused.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Canonical category of the system template.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Title).HasColumnType("nvarchar(120)").HasComment("Current public system-quest title; accepted terms use immutable account definition revisions.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CatalogVersion).HasColumnType("varchar(32)").HasComment("Reviewed template version; seeds must agree with the canonical catalog.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => row.CategoryId, "IX_SystemQuest_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemQuest_Category");
    }
}
