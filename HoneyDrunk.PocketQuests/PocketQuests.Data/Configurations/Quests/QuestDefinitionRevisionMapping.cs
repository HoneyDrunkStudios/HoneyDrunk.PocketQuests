using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestDefinitionRevision fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestDefinitionRevisionMapping : IEntityTypeConfiguration<QuestDefinitionRevisionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestDefinitionRevisionEntity> entity)
    {
        entity.ToTable("QuestDefinitionRevision", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable content/reward version of an account quest definition. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_BaseXp", "[BaseXp] >= 0");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_RankCode", "[RankCode] IN ('F','E','D','C','B','A','S')");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_EffortCode", "[EffortCode] IN ('Small','Medium','Large')");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_PenaltyPercent", "[PenaltyPercent] IN (0,10,25,50)");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_Text", "LEN([Title]) BETWEEN 1 AND 120 AND LEN([Criterion]) BETWEEN 1 AND 2000 AND DATALENGTH([Title])=DATALENGTH(TRIM([Title])) AND DATALENGTH([Criterion])=DATALENGTH(TRIM([Criterion]))");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_DisplayDocument", "[DisplaySnapshotVersion]>=1 AND ISJSON([DisplaySnapshotJson])=1 AND DATALENGTH([DisplaySnapshotJson])<=65536");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_QuestDefinitionRevision_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestDefinitionRevision").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestDefinitionRevision_AccountId_Id");
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId, row.Revision }, "UQ_QuestDefinitionRevision_AccountId_QuestDefinitionId_Revision").IsUnique().HasFilter(null);
        entity.HasAlternateKey(row => new { row.AccountId, row.QuestDefinitionId, row.Id }).HasName("UQ_QuestDefinitionRevision_AccountId_QuestDefinitionId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionId).HasComment("Definition to which this immutable revision belongs.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Revision).HasComment("Positive sequence within this definition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Title).HasComment("Frozen trimmed quest title, 1 to 120 characters.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Criterion).HasComment("Frozen self-attestation criterion, 1 to 2000 characters; may contain personal content.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(2000).IsUnicode(true).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Description).HasComment("Frozen optional quest explanation. Null means none was supplied.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(2000).IsUnicode(true).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CategoryId).HasComment("Frozen category of this content revision.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.RankCode).HasColumnType("char(1)").HasComment("Frozen quest rank F/E/D/C/B/A/S.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(1).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EffortCode).HasComment("Frozen Small, Medium or Large effort tier.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(6).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.BaseXp).HasComment("Calibrated nonnegative base XP under the frozen ruleset; never client-authoritative.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.PenaltyPercent).HasComment("Proposed penalty percentage for new acceptance: 0, 10, 25 or 50. Accepted losses are separately locked.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.RulesetVersion).HasComment("Frozen ruleset version used to derive rewards.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DisplaySnapshotVersion).HasComment("Schema version of the immutable display-label document.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DisplaySnapshotJson).HasComment("Frozen human-readable names for historical display/export only; typed references and allocations enforce rules. Maximum 65536 UTF-16 bytes.").UseCollation("Latin1_General_100_BIN2").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EffectiveAt).HasComment("Time the new content revision became effective. UTC instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandReceiptId).HasComment("Receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => row.CategoryId, "IX_QuestDefinitionRevision_FK_Category");
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestDefinitionRevision_FK_CommandReceipt");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionRevision_Account");
        entity.HasOne<QuestDefinitionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionRevision_QuestDefinition");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionRevision_Category");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestDefinitionRevision_CommandReceipt");
    }
}
