using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestOccurrenceEvent fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestOccurrenceEventMapping : IEntityTypeConfiguration<QuestOccurrenceEventEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestOccurrenceEventEntity> entity)
    {
        entity.ToTable("QuestOccurrenceEvent", "pocketquests", table =>
        {
            table.HasComment("One row is one effective occurrence transition including completion, Undo or clock assessment. Classification: Restricted. History: event; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_QuestOccurrenceEvent_EventCode", "[EventCode] IN ('Accepted','Offered','Edited','Frozen','Resumed','Abandoned','Completed','Undone','DeadlineElapsed')");
            table.HasCheckConstraint("CK_QuestOccurrenceEvent_AccountMutationVersion", "[AccountMutationVersion] >= 0");
            table.HasCheckConstraint("CK_QuestOccurrenceEvent_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_QuestOccurrenceEvent_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestOccurrenceEvent").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestOccurrenceEvent_AccountId_Id");
        entity.HasAlternateKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).HasName("UQ_QuestOccurrenceEvent_AccountId_QuestOccurrenceId_Id");
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Stable effective event UUID, reused as the completion ID for Completed events.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Occurrence affected by this transition.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.QuestOccurrenceRevisionId).HasColumnType("uniqueidentifier").HasComment("Immutable occurrence version relevant to the transition.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EventCode).HasColumnType("varchar(20)").HasComment("Accepted, Offered, Edited, Frozen, Resumed, Abandoned, Completed, Undone or DeadlineElapsed.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(20).IsUnicode(false).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.EffectiveAt).HasColumnType("datetimeoffset(7)").HasComment("Domain action/assessment time; never substituted with server receipt time. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountMutationVersion).HasColumnType("bigint").HasComment("Commit visibility order of this event.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandReceiptId).HasColumnType("uniqueidentifier").HasComment("Receipt for the command that caused this fact. Null means trusted clock/lifecycle reconciliation, not a client command.").IsRequired(false).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasIndex(row => new { row.AccountId, row.EffectiveAt, row.Id }, "IX_QuestOccurrenceEvent_Replay").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId, row.QuestOccurrenceRevisionId }, "IX_QuestOccurrenceEvent_FK_QuestOccurrenceRevision").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestOccurrenceEvent_FK_CommandReceipt").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceEvent_Account");
        entity.HasOne<QuestOccurrenceRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId, row.QuestOccurrenceRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.QuestOccurrenceId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceEvent_QuestOccurrenceRevision");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestOccurrenceEvent_CommandReceipt");
    }
}
