using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Mappings.Quests;

/// <summary>Maps QuestOccurrenceRevision fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestOccurrenceRevisionMapping : IEntityTypeConfiguration<QuestOccurrenceRevisionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestOccurrenceRevisionEntity> entity)
    {
        entity.ToTable("QuestOccurrenceRevision", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable occurrence version visible at a committed account mutation. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_StateCode", "[StateCode] IN ('Active','Completed','Missed','Frozen','Abandoned','Offered')");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_ScheduleNulls", "([DueOn] IS NULL AND [DeadlineAt] IS NULL AND [PlannedTime] IS NULL) OR ([DueOn] IS NOT NULL AND [DeadlineAt] IS NOT NULL AND [DeadlineTimeZoneId] IS NOT NULL AND [DueOn]<CONVERT(date,'9999-01-01'))");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_LockedPenalty", "([LockedLoss] IS NULL AND [LossCategoryId] IS NULL) OR ([LockedLoss] IS NOT NULL AND [LockedLoss]>=0 AND [LossCategoryId] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_Acceptance", "([StateCode]='Offered' AND [AcceptedAt] IS NULL AND [LockedLoss] IS NULL) OR ([StateCode]<>'Offered' AND [AcceptedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_FreezeFlag", "[IsIndividuallyFrozen]=0 OR [FrozenAt] IS NOT NULL");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_AccountMutationVersion", "[AccountMutationVersion] >= 0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_DeadlineAtUtc", "[DeadlineAt] IS NULL OR DATEPART(TZOFFSET,[DeadlineAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_AcceptedAtUtc", "[AcceptedAt] IS NULL OR DATEPART(TZOFFSET,[AcceptedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_FrozenAtUtc", "[FrozenAt] IS NULL OR DATEPART(TZOFFSET,[FrozenAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_AbandonedAtUtc", "[AbandonedAt] IS NULL OR DATEPART(TZOFFSET,[AbandonedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_OriginatedAtUtc", "[OriginatedAt] IS NULL OR DATEPART(TZOFFSET,[OriginatedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceRevision_OriginatedOffset", "[OriginatedOffsetMinutes]>=-840 AND [OriginatedOffsetMinutes]<=840");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestOccurrenceRevision").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestOccurrenceRevision_AccountId_Id");
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.Revision }, "UQ_QuestOccurrenceRevision_AccountId_QuestOccurrenceId_Revision").IsUnique().HasFilter(null);
        entity.HasAlternateKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).HasName("UQ_QuestOccurrenceRevision_AccountId_QuestOccurrenceId_Id");
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Occurrence whose terms and scheduling state this version captures.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Revision).HasColumnType("int").HasComment("Positive version within this occurrence.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestDefinitionId).HasColumnType("uniqueidentifier").HasComment("Persistent account-owned definition identity.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestDefinitionRevisionId).HasColumnType("uniqueidentifier").HasComment("Exact immutable content/reward revision used by this occurrence version.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Materialized current/frozen category for filtering, constrained to the controlled revision write.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.DueOn).HasColumnType("date").HasComment("Due date in DeadlineTimeZoneId; null means unscheduled with no deadline.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.PlannedTime).HasColumnType("time(0)").HasComment("Optional local planned clock time; null means none; requires DueOn.").HasPrecision(0).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.DeadlineAt).HasColumnType("datetimeoffset(7)").HasComment("Absolute end-of-due-day deadline; null for unscheduled occurrence. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.DeadlineTimeZoneId).HasColumnType("varchar(100)").HasComment("IANA calendar zone used to calculate DueOn/DeadlineAt. Null only for unscheduled occurrence.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.StateCode).HasColumnType("varchar(10)").HasComment("Active, Completed, Missed, Frozen, Abandoned or Offered; time-dependent projection can be newer than persisted reconciliation.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(10).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AcceptedAt).HasColumnType("datetimeoffset(7)").HasComment("Recorded acceptance time; null while only an unaccepted offer. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.FrozenAt).HasColumnType("datetimeoffset(7)").HasComment("Start of the current freeze, if any. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.IsIndividuallyFrozen).HasColumnType("bit").HasComment("Whether an individual freeze remains after account/category resume.").IsRequired(true).HasDefaultValueSql("0");
        entity.Property(row => row.AbandonedAt).HasColumnType("datetimeoffset(7)").HasComment("Explicit abandonment time, if any. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.LockedLoss).HasColumnType("bigint").HasComment("Exact accepted nonnegative penalty amount. Null means no penalty commitment.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.LossCategoryId).HasColumnType("varchar(40)").HasComment("Category frozen with an accepted loss; null exactly when LockedLoss is null.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.AccountMutationVersion).HasColumnType("bigint").HasComment("Visibility order at commit; distinct from backdated EffectiveAt.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.EffectiveAt).HasColumnType("datetimeoffset(7)").HasComment("Recorded effective time of the transition. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CommandReceiptId).HasColumnType("uniqueidentifier").HasComment("Receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.OriginatedAt).HasColumnType("datetimeoffset(7)").HasComment("Original occurrence creation/delivery instant used by the domain even while the recurring occurrence is still an unaccepted offer. Null means not applicable to this transition.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.OriginatedOffsetMinutes).HasColumnType("smallint").HasComment("Original domain/API display offset of OriginatedAt, retained separately from its authoritative UTC instant for exact recurring-offer response reconstruction.").IsRequired(true).HasDefaultValueSql("0");
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.AccountMutationVersion, row.Id }, "IX_QuestOccurrenceRevision_AnchorVisibility").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }, "IX_QuestOccurrenceRevision_FK_QuestDefinitionRevision").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.CategoryId, "IX_QuestOccurrenceRevision_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.LossCategoryId, "IX_QuestOccurrenceRevision_FK_Category_Loss").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestOccurrenceRevision_FK_CommandReceipt").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_Account");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestDefinitionId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_QuestDefinitionRevision");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_Category");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.LossCategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_Category_Loss");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_QuestOccurrence");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceRevision_CommandReceipt");
    }
}
