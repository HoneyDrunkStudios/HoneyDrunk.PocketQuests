using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestOccurrence fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestOccurrenceMapping : IEntityTypeConfiguration<QuestOccurrenceEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestOccurrenceEntity> entity)
    {
        entity.ToTable("QuestOccurrence", "pocketquests", table =>
        {
            table.HasComment("One row is one current offered or accepted occurrence, with immutable past versions below. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestOccurrence_StateCode", "[StateCode] IN ('Active','Completed','Missed','Frozen','Abandoned','Offered')");
            table.HasCheckConstraint("CK_QuestOccurrence_ScheduleNulls", "([DueOn] IS NULL AND [DeadlineAt] IS NULL AND [PlannedTime] IS NULL) OR ([DueOn] IS NOT NULL AND [DeadlineAt] IS NOT NULL AND [DeadlineTimeZoneId] IS NOT NULL AND [DueOn]<CONVERT(date,'9999-01-01'))");
            table.HasCheckConstraint("CK_QuestOccurrence_LockedPenalty", "([LockedLoss] IS NULL AND [LossCategoryId] IS NULL) OR ([LockedLoss] IS NOT NULL AND [LockedLoss]>=0 AND [LossCategoryId] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestOccurrence_Acceptance", "([StateCode]='Offered' AND [AcceptedAt] IS NULL AND [LockedLoss] IS NULL) OR ([StateCode]<>'Offered' AND [AcceptedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestOccurrence_FreezeFlag", "[IsIndividuallyFrozen]=0 OR [FrozenAt] IS NOT NULL");
            table.HasCheckConstraint("CK_QuestOccurrence_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_QuestOccurrence_SeriesFields", "([QuestSeriesId] IS NULL AND [QuestSeriesRevisionId] IS NULL AND [SeriesSequence] IS NULL) OR ([QuestSeriesId] IS NOT NULL AND [QuestSeriesRevisionId] IS NOT NULL AND [SeriesSequence] IS NOT NULL AND [SeriesSequence]>=0)");
            table.HasCheckConstraint("CK_QuestOccurrence_NotOwnParent", "[ParentQuestOccurrenceId] IS NULL OR [ParentQuestOccurrenceId]<>[Id]");
            table.HasCheckConstraint("CK_QuestOccurrence_DeadlineAtUtc", "[DeadlineAt] IS NULL OR DATEPART(TZOFFSET,[DeadlineAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_AcceptedAtUtc", "[AcceptedAt] IS NULL OR DATEPART(TZOFFSET,[AcceptedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_FrozenAtUtc", "[FrozenAt] IS NULL OR DATEPART(TZOFFSET,[FrozenAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_AbandonedAtUtc", "[AbandonedAt] IS NULL OR DATEPART(TZOFFSET,[AbandonedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_QuestOccurrence_OriginatedAtUtc", "[OriginatedAt] IS NULL OR DATEPART(TZOFFSET,[OriginatedAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrence_CreationOrdinal", "[CreationOrdinal]>=0");
            table.HasCheckConstraint("CK_QuestOccurrence_OriginatedOffset", "[OriginatedOffsetMinutes]>=-840 AND [OriginatedOffsetMinutes]<=840");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestOccurrence").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestOccurrence_AccountId_Id");
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionId).HasComment("Persistent account-owned definition identity.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestDefinitionRevisionId).HasComment("Exact immutable content/reward revision used by this occurrence version.");
        entity.Property(row => row.CategoryId).HasComment("Materialized current/frozen category for filtering, constrained to the controlled revision write.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.DueOn).HasComment("Due date in DeadlineTimeZoneId; null means unscheduled with no deadline.");
        entity.Property(row => row.PlannedTime).HasColumnType("time(0)").HasComment("Optional local planned clock time; null means none; requires DueOn.").HasPrecision(0);
        entity.Property(row => row.DeadlineAt).HasComment("Absolute end-of-due-day deadline; null for unscheduled occurrence. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.DeadlineTimeZoneId).HasComment("IANA calendar zone used to calculate DueOn/DeadlineAt. Null only for unscheduled occurrence.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false);
        entity.Property(row => row.StateCode).HasComment("Active, Completed, Missed, Frozen, Abandoned or Offered; time-dependent projection can be newer than persisted reconciliation.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(10).IsUnicode(false);
        entity.Property(row => row.AcceptedAt).HasComment("Recorded acceptance time; null while only an unaccepted offer. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.FrozenAt).HasComment("Start of the current freeze, if any. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.IsIndividuallyFrozen).HasComment("Whether an individual freeze remains after account/category resume.").HasDefaultValueSql("0");
        entity.Property(row => row.AbandonedAt).HasComment("Explicit abandonment time, if any. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.LockedLoss).HasComment("Exact accepted nonnegative penalty amount. Null means no penalty commitment.");
        entity.Property(row => row.LossCategoryId).HasComment("Category frozen with an accepted loss; null exactly when LockedLoss is null.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false);
        entity.Property(row => row.Revision).HasComment("Positive immutable occurrence-version number matching this current row.");
        entity.Property(row => row.QuestSeriesId).HasComment("Recurrence source; null for a one-off quest.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestSeriesRevisionId).HasComment("Exact recurrence configuration; null for a one-off quest.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.SeriesSequence).HasComment("Nonnegative sequence within the source series version; null for a one-off quest.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ParentQuestOccurrenceId).HasComment("Optional independently attested Large parent owned by the same account; null means no parent.");
        entity.Property(row => row.SourceSyncAnchorId).HasComment("Proof under which an occurrence was created offline; null for ordinary online creation.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.OriginatedAt).HasComment("Original occurrence creation/delivery instant used by the domain even while the recurring occurrence is still an unaccepted offer. Null means not applicable to this transition.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreationOrdinal).HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OriginatedOffsetMinutes).HasComment("Original domain/API display offset of OriginatedAt, retained separately from its authoritative UTC instant for exact recurring-offer response reconstruction.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId, row.SeriesSequence }, "UQ_QuestOccurrence_SeriesDelivery").IsUnique().HasFilter("[QuestSeriesId] IS NOT NULL");
        entity.HasIndex(row => new { row.AccountId, row.StateCode, row.DueOn, row.Id }, "IX_QuestOccurrence_QuestList");
        entity.HasIndex(row => new { row.AccountId, row.StateCode, row.DeadlineAt, row.Id }, "IX_QuestOccurrence_DueWork");
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }, "IX_QuestOccurrence_FK_QuestDefinitionRevision");
        entity.HasIndex(row => row.CategoryId, "IX_QuestOccurrence_FK_Category");
        entity.HasIndex(row => row.LossCategoryId, "IX_QuestOccurrence_FK_Category_Loss");
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId }, "IX_QuestOccurrence_FK_QuestSeriesRevision");
        entity.HasIndex(row => new { row.AccountId, row.ParentQuestOccurrenceId }, "IX_QuestOccurrence_FK_QuestOccurrence_Parent");
        entity.HasIndex(row => new { row.AccountId, row.SourceSyncAnchorId }, "IX_QuestOccurrence_FK_SyncAnchor_Source");
        entity.HasIndex(row => new { row.AccountId, row.CreationOrdinal }, "UX_QuestOccurrence_CreationPage").IsUnique().HasFilter("[CreationOrdinal]>0");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Account");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestDefinitionId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestDefinitionRevision");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Category");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.LossCategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Category_Loss");
        entity.HasOne<QuestSeriesRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestSeriesId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestSeriesRevision");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.ParentQuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestOccurrence_Parent");
        entity.HasOne<SyncAnchorEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.SourceSyncAnchorId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_SyncAnchor_Source");
    }
}
