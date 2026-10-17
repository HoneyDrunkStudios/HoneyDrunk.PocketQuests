using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestDefinition fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestDefinitionMapping : IEntityTypeConfiguration<QuestDefinitionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestDefinitionEntity> entity)
    {
        entity.ToTable("QuestDefinition", "pocketquests", table =>
        {
            table.HasComment("One row is one account-owned persistent quest identity, custom or adopted from the system catalog. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestDefinition_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestDefinition_ArchivedAtUtc", "[ArchivedAt] IS NULL OR DATEPART(TZOFFSET,[ArchivedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinition_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinition_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_QuestDefinition_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_QuestDefinition_CreationOrdinal", "[CreationOrdinal]>=0");
            table.HasCheckConstraint("CK_QuestDefinition_ClientKey", "[ClientKey] IS NULL OR (DATALENGTH([ClientKey])=36 AND TRY_CAST([ClientKey] AS uniqueidentifier) IS NOT NULL AND TRY_CAST([ClientKey] AS uniqueidentifier)=[Id])");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestDefinition").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestDefinition_AccountId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.SystemQuestId).HasComment("System template identity; null means an editable custom definition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Revision).HasComment("Current content revision number; matching immutable revision is committed by the controlled writer.");
        entity.Property(row => row.ArchivedAt).HasComment("Instant this definition was archived; existing occurrences retain history. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ClientKey).HasComment("Original accepted UUID spelling for API compatibility; the typed Id remains the ownership key. Null is reserved for public catalog adoption or the retained v1 adapter.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(36).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.SystemQuestId }, "UQ_QuestDefinition_SystemIdentity").IsUnique().HasFilter("[SystemQuestId] IS NOT NULL");
        entity.HasIndex(row => row.SystemQuestId, "IX_QuestDefinition_FK_SystemQuest");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinition_Account");
        entity.HasOne<SystemQuestEntity>().WithMany().HasForeignKey(row => row.SystemQuestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinition_SystemQuest");
    }
}
