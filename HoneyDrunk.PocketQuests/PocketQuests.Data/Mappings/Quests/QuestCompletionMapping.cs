using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Mappings.Quests;

/// <summary>Maps QuestCompletion fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestCompletionMapping : IEntityTypeConfiguration<QuestCompletionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestCompletionEntity> entity)
    {
        entity.ToTable("QuestCompletion", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable successful completion with at most one write-once Undo annotation. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestCompletion_Undo", "([UndoneAt] IS NULL AND [UndoQuestOccurrenceEventId] IS NULL) OR ([UndoneAt] IS NOT NULL AND [UndoQuestOccurrenceEventId] IS NOT NULL AND [UndoneAt]>=[RecordedAt] AND [UndoneAt]<DATEADD(hour,24,[RecordedAt]))");
            table.HasCheckConstraint("CK_QuestCompletion_RecordedAtUtc", "DATEPART(TZOFFSET,[RecordedAt])=0");
            table.HasCheckConstraint("CK_QuestCompletion_UndoneAtUtc", "[UndoneAt] IS NULL OR DATEPART(TZOFFSET,[UndoneAt])=0");
            table.HasCheckConstraint("CK_QuestCompletion_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestCompletion_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_QuestCompletion_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestCompletion").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestCompletion_AccountId_Id");
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }, "UQ_QuestCompletion_AccountId_QuestOccurrenceId_Id").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("UUID of its Completed occurrence event and stable completion identifier.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Occurrence completed by this fact.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestOccurrenceRevisionId).HasColumnType("uniqueidentifier").HasComment("Exact completed terms; never replaced by a later definition edit.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RecordedAt).HasColumnType("datetimeoffset(7)").HasComment("Trusted recorded completion time, including validated offline time. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.UndoneAt).HasColumnType("datetimeoffset(7)").HasComment("Trusted recorded Undo time, if the completion was reversed. UTC instant. Null means this event has not happened.").HasPrecision(7).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.UndoQuestOccurrenceEventId).HasColumnType("uniqueidentifier").HasComment("Stable Undone event; null exactly when UndoneAt is null.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRequired(true).IsRowVersion();
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId }, "UQ_QuestCompletion_OneLive").IsUnique(true).HasFilter("[UndoneAt] IS NULL");
        entity.HasIndex(row => new { row.AccountId, row.UndoQuestOccurrenceEventId }, "UQ_QuestCompletion_OneUndo").IsUnique(true).HasFilter("[UndoQuestOccurrenceEventId] IS NOT NULL");
        entity.HasIndex(row => new { row.AccountId, row.RecordedAt, row.Id }, "IX_QuestCompletion_History").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.QuestOccurrenceRevisionId }, "IX_QuestCompletion_FK_QuestOccurrenceRevision").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.UndoQuestOccurrenceEventId }, "IX_QuestCompletion_FK_QuestOccurrenceEvent_Undo").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCompletion_Account");
        entity.HasOne<QuestOccurrenceEventEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).HasPrincipalKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCompletion_QuestOccurrenceEvent_Completion");
        entity.HasOne<QuestOccurrenceRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId, row.QuestOccurrenceRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCompletion_QuestOccurrenceRevision");
        entity.HasOne<QuestOccurrenceEventEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId, row.UndoQuestOccurrenceEventId }).HasPrincipalKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCompletion_QuestOccurrenceEvent_Undo");
    }
}
