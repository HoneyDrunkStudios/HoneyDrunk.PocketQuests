using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Mappings.Quests;

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
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
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
        entity.Property(row => row.Revision).HasColumnType("int").HasComment("Positive immutable occurrence-version number matching this current row.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestSeriesId).HasColumnType("uniqueidentifier").HasComment("Recurrence source; null for a one-off quest.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.QuestSeriesRevisionId).HasColumnType("uniqueidentifier").HasComment("Exact recurrence configuration; null for a one-off quest.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SeriesSequence).HasColumnType("int").HasComment("Nonnegative sequence within the source series version; null for a one-off quest.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ParentQuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Optional independently attested Large parent owned by the same account; null means no parent.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SourceSyncAnchorId).HasColumnType("uniqueidentifier").HasComment("Proof under which an occurrence was created offline; null for ordinary online creation.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRequired(true).IsRowVersion();
        entity.Property(row => row.OriginatedAt).HasColumnType("datetimeoffset(7)").HasComment("Original occurrence creation/delivery instant used by the domain even while the recurring occurrence is still an unaccepted offer. Null means not applicable to this transition.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreationOrdinal).HasColumnType("int").HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").IsRequired(true).HasDefaultValueSql("0");
        entity.Property(row => row.OriginatedOffsetMinutes).HasColumnType("smallint").HasComment("Original domain/API display offset of OriginatedAt, retained separately from its authoritative UTC instant for exact recurring-offer response reconstruction.").IsRequired(true).HasDefaultValueSql("0");
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId, row.SeriesSequence }, "UQ_QuestOccurrence_SeriesDelivery").IsUnique(true).HasFilter("[QuestSeriesId] IS NOT NULL");
        entity.HasIndex(row => new { row.AccountId, row.StateCode, row.DueOn, row.Id }, "IX_QuestOccurrence_QuestList").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.StateCode, row.DeadlineAt, row.Id }, "IX_QuestOccurrence_DueWork").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }, "IX_QuestOccurrence_FK_QuestDefinitionRevision").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.CategoryId, "IX_QuestOccurrence_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.LossCategoryId, "IX_QuestOccurrence_FK_Category_Loss").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId }, "IX_QuestOccurrence_FK_QuestSeriesRevision").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.ParentQuestOccurrenceId }, "IX_QuestOccurrence_FK_QuestOccurrence_Parent").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.SourceSyncAnchorId }, "IX_QuestOccurrence_FK_SyncAnchor_Source").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CreationOrdinal }, "UX_QuestOccurrence_CreationPage").IsUnique(true).HasFilter("[CreationOrdinal]>0");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Account");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestDefinitionId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestDefinitionRevision");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Category");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.LossCategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_Category_Loss");
        entity.HasOne<QuestSeriesRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestSeriesId, row.QuestSeriesRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestSeriesId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestSeriesRevision");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.ParentQuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_QuestOccurrence_Parent");
        entity.HasOne<SyncAnchorEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.SourceSyncAnchorId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrence_SyncAnchor_Source");
    }
}
