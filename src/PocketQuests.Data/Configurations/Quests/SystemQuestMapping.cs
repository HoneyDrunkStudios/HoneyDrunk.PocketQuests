using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps SystemQuest fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class SystemQuestMapping : IEntityTypeConfiguration<SystemQuestEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SystemQuestEntity> entity)
    {
        entity.ToTable("SystemQuest", "pocketquests", table =>
        {
            table.HasComment("One row is one stable system quest identity. Classification: Public except Restricted operator identifiers. History: mutable draft and publish pointers with rowversion; immutable SystemQuestRevision retains authored content and shared Audit records actions. At most 200 operator-authored identities; retain referenced identities and revisions, never reuse codes. Operator identifiers are cleared on their account erasure.");
            table.HasCheckConstraint("CK_SystemQuest_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_SystemQuest_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_SystemQuest_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_SystemQuest_CreatedBy", "[CreatedBy] IS NULL OR (DATALENGTH([CreatedBy])=30 AND LEFT([CreatedBy],4)='usr_' AND SUBSTRING([CreatedBy],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%')");
            table.HasCheckConstraint("CK_SystemQuest_ModifiedBy", "[ModifiedBy] IS NULL OR (DATALENGTH([ModifiedBy])=30 AND LEFT([ModifiedBy],4)='usr_' AND SUBSTRING([ModifiedBy],5,26) NOT LIKE '%[^0123456789ABCDEFGHJKMNPQRSTVWXYZ]%')");
        });
        entity.Property(row => row.DraftSystemQuestRevisionId).HasComment("Latest saved immutable draft revision of this same system quest. Null for source-controlled starter templates or during atomic initial creation.");
        entity.Property(row => row.PublishedSystemQuestRevisionId).HasComment("Published immutable revision of this same system quest. Null means operator-authored quest is not discoverable; source-controlled starters remain in code.");
        entity.Property(row => row.RowVersion).HasComment("SQL concurrency token for explicit draft and publishing changes, not a timestamp.").IsRowVersion();
        entity.Property(row => row.CreatedBy).HasComment("Restricted canonical Identity operator usr_ identifier; null for source-controlled templates or after operator account erasure.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false);
        entity.Property(row => row.ModifiedBy).HasComment("Restricted canonical Identity operator usr_ identifier; null for source-controlled templates or after operator account erasure.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(30).IsUnicode(false);
        entity.HasIndex(row => new { row.Id, row.DraftSystemQuestRevisionId }, "IX_SystemQuest_DraftRevision");
        entity.HasOne<SystemQuestRevisionEntity>().WithMany().HasForeignKey(row => new { row.Id, row.DraftSystemQuestRevisionId }).HasPrincipalKey(row => new { row.SystemQuestId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemQuest_SystemQuestRevision_Draft");
        entity.HasIndex(row => new { row.Id, row.PublishedSystemQuestRevisionId }, "IX_SystemQuest_PublishedRevision");
        entity.HasOne<SystemQuestRevisionEntity>().WithMany().HasForeignKey(row => new { row.Id, row.PublishedSystemQuestRevisionId }).HasPrincipalKey(row => new { row.SystemQuestId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemQuest_SystemQuestRevision_Published");
        entity.HasKey(row => row.Id).HasName("PK_SystemQuest").IsClustered();
        entity.Property(row => row.Id).HasComment("Stable system quest code, for example PQ-CAT-Q01; never reused.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasComment("Canonical category of the system template.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.Title).HasComment("Current public system-quest title; accepted terms use immutable account definition revisions.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true);
        entity.Property(row => row.CatalogVersion).HasComment("Reviewed template version; seeds must agree with the canonical catalog.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => row.CategoryId, "IX_SystemQuest_FK_Category");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SystemQuest_Category");
    }
}
