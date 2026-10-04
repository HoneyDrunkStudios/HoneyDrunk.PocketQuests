using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Mappings.Quests;

/// <summary>Maps QuestCommandHistory fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestCommandHistoryMapping : IEntityTypeConfiguration<QuestCommandHistoryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestCommandHistoryEntity> entity)
    {
        entity.ToTable("QuestCommandHistory", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable, typed successful action input and its replay clocks, never a serialized command or account snapshot. Classification: Restricted. History: immutable. Erase with its account; no age cutoff on replay history.");
            table.HasCheckConstraint("CK_QuestCommandHistory_Action", "[ActionCode] IN ('accept','complete','undo','save-definition','archive-definition','assess-skill','save-skill','archive-skill','interests','finish-onboarding','plan','zone','expiry-warnings','link','select-badge','select-frame','save-series','stop-series','pause','resume','resume-occurrence','abandon','accept-offer','$reconcile','$lifecycle-pause')");
            table.HasCheckConstraint("CK_QuestCommandHistory_Version", "[AccountMutationVersion]>0 AND ([ExpectedRevision] IS NULL OR [ExpectedRevision]>=0)");
            table.HasCheckConstraint("CK_QuestCommandHistory_SkillTarget", "[SystemSkillId] IS NULL OR [CustomSkillId] IS NULL");
            table.HasCheckConstraint("CK_QuestCommandHistory_Receipt", "([ActionCode] IN ('$reconcile','$lifecycle-pause') AND [CommandReceiptId] IS NULL) OR ([ActionCode]<>'$reconcile' AND [ActionCode]<>'$lifecycle-pause' AND [CommandReceiptId] IS NOT NULL AND [CommandReceiptId]=[Id])");
            table.HasCheckConstraint("CK_QuestCommandHistory_Cadence", "[CadenceCode] IS NULL OR [CadenceCode] IN ('Days','Weeks','Months','Years')");
            table.HasCheckConstraint("CK_QuestCommandHistory_Interval", "[Interval] IS NULL OR ([Interval]>=1 AND [Interval]<=999)");
            table.HasCheckConstraint("CK_QuestCommandHistory_Experience", "[ExperienceCode] IS NULL OR [ExperienceCode] IN ('New','Practiced','Experienced','Expert')");
            table.HasCheckConstraint("CK_QuestCommandHistory_Clocks", "[ProjectionAt]>=[ReconciledAt] AND [ProjectionAt]>=[RecordedAt]");
            table.HasCheckConstraint("CK_QuestCommandHistory_ReconciledAtUtc", "DATEPART(TZOFFSET,[ReconciledAt])=0");
            table.HasCheckConstraint("CK_QuestCommandHistory_RecordedAtUtc", "DATEPART(TZOFFSET,[RecordedAt])=0");
            table.HasCheckConstraint("CK_QuestCommandHistory_ProjectionAtUtc", "DATEPART(TZOFFSET,[ProjectionAt])=0");
            table.HasCheckConstraint("CK_QuestCommandHistory_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_QuestCommandHistory_ReconciliationLimit", "[ReconciliationLimit]>=0");
            table.HasCheckConstraint("CK_QuestCommandHistory_ActionReconciliationLimit", "[ActionReconciliationLimit]>=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_QuestCommandHistory").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestCommandHistory_Account_Id");
        entity.HasIndex(row => new { row.AccountId, row.AccountMutationVersion }, "UQ_QuestCommandHistory_Account_Version").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Stable transition UUID, equal to the operation UUID for a client command.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Owning personal account; all references use this same ownership boundary.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CommandReceiptId).HasColumnType("uniqueidentifier").HasComment("Receipt for a client command; null for an internal reconciliation or lifecycle transition. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.AccountMutationVersion).HasColumnType("bigint").HasComment("Committed account sequence used to reconstruct a receipt or an issued sync anchor.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ActionCode).HasColumnType("varchar(40)").HasComment("Stable command action or the documented internal reconciliation/lifecycle action.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RulesetVersion).HasColumnType("varchar(32)").HasComment("Retained domain replay implementation version; never silently replay through a different ruleset.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.TimeZoneBefore).HasColumnType("varchar(100)").HasComment("IANA zone immediately before this transition; the first row preserves the initial account zone.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ReconciledAt).HasColumnType("datetimeoffset(7)").HasComment("UTC clock through which due series deliveries were reconciled before applying the action.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RecordedAt).HasColumnType("datetimeoffset(7)").HasComment("Verified UTC effective action time; may precede receipt by any duration.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ProjectionAt).HasColumnType("datetimeoffset(7)").HasComment("Original UTC response projection time, independent of later retries.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Target or server-resolved new occurrence identity. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.QuestCompletionId).HasColumnType("uniqueidentifier").HasComment("Completion selected by Undo. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.QuestDefinitionRevisionId).HasColumnType("uniqueidentifier").HasComment("Immutable quest terms supplied to a definition, acceptance, series or offer command. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CompletionTermsRevisionId).HasColumnType("uniqueidentifier").HasComment("Frozen occurrence revision actually used for a delayed completion after a later edit. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ExpectedRevision).HasColumnType("int").HasComment("Optimistic public definition, skill or series revision supplied by the successful command. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SystemSkillId).HasColumnType("varchar(40)").HasComment("System skill selected for assessment. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CustomSkillId).HasColumnType("uniqueidentifier").HasComment("Owned skill selected for creation, editing, archival or assessment. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SkillName).HasColumnType("nvarchar(80)").HasComment("Validated name supplied by a skill save command; this is historical input, not the mutable current name. Null means not applicable to this transition.").HasMaxLength(80).IsUnicode(true).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ExperienceCode).HasColumnType("varchar(12)").HasComment("Validated experience selected for an assessment. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.DueOn).HasColumnType("date").HasComment("Requested due or first-delivery local date. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.PlannedTime).HasColumnType("time(0)").HasComment("Requested local hour and minute. Null means not applicable to this transition.").HasPrecision(0).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ParentQuestOccurrenceId).HasColumnType("uniqueidentifier").HasComment("Owned parent selected by a link command. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ProfileRewardId).HasColumnType("varchar(40)").HasComment("Selected badge or frame; null also records an explicit clear. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.QuestSeriesId).HasColumnType("uniqueidentifier").HasComment("Owned recurrence selected for creation, edit or stop. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CadenceCode).HasColumnType("varchar(6)").HasComment("Requested recurrence unit. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(6).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.Interval).HasColumnType("int").HasComment("Requested positive recurrence interval. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Category pause/resume target; null means the whole account. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ConfirmPenalty).HasColumnType("bit").HasComment("Whether the successful command explicitly confirmed penalty terms.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AcceptedLoss).HasColumnType("int").HasComment("Exact penalty amount accepted by the command. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.HasAcceptedTerms).HasColumnType("bit").HasComment("Whether the client supplied accepted quest terms; reconstruct them from the immutable typed terms reference.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.NewTimeZone).HasColumnType("varchar(100)").HasComment("Requested IANA zone for a zone-change command. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ExpectedTimeZone).HasColumnType("varchar(100)").HasComment("Optimistic previous IANA zone supplied by the command. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.ConfirmZoneChange).HasColumnType("bit").HasComment("Whether a successful zone change was explicitly confirmed.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.HasExpiryWarnings).HasColumnType("bit").HasComment("Requested expiry-warning preference. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SourceSyncAnchorId).HasColumnType("uniqueidentifier").HasComment("Original proof anchor, retaining offline acceptance provenance without copying an anchor snapshot. Null means not applicable to this transition.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("UTC server insertion instant; never substituted for the effective action clock.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ReconciliationLimit).HasColumnType("int").HasComment("Maximum due-delivery cursor steps performed before this action; zero records lifecycle transitions that intentionally do not reconcile before freezing. Retain this input for exact partial-batch replay.").IsRequired(true).HasDefaultValueSql("2147483647");
        entity.Property(row => row.ActionReconciliationLimit).HasColumnType("int").HasComment("Maximum due-delivery cursor work performed by the action itself, such as a zone change or series creation; retained independently of the pre-action budget for exact original-response replay.").IsRequired(true).HasDefaultValueSql("2147483647");
        entity.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestCommandHistory_CommandReceiptId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId }, "IX_QuestCommandHistory_QuestOccurrenceId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestCompletionId }, "IX_QuestCommandHistory_QuestCompletionId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestDefinitionRevisionId }, "IX_QuestCommandHistory_QuestDefinitionRevisionId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CompletionTermsRevisionId }, "IX_QuestCommandHistory_CompletionTermsRevisionId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_QuestCommandHistory_CustomSkillId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.ParentQuestOccurrenceId }, "IX_QuestCommandHistory_ParentQuestOccurrenceId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestSeriesId }, "IX_QuestCommandHistory_QuestSeriesId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.SourceSyncAnchorId }, "IX_QuestCommandHistory_SourceSyncAnchorId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.SystemSkillId, "IX_QuestCommandHistory_SystemSkillId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.ProfileRewardId, "IX_QuestCommandHistory_ProfileRewardId").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.CategoryId, "IX_QuestCommandHistory_CategoryId").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Account");
        entity.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CommandReceiptId");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestOccurrenceId");
        entity.HasOne<QuestCompletionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestCompletionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestCompletionId");
        entity.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestDefinitionRevisionId");
        entity.HasOne<QuestOccurrenceRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CompletionTermsRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CompletionTermsRevisionId");
        entity.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CustomSkillId");
        entity.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.ParentQuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_ParentQuestOccurrenceId");
        entity.HasOne<QuestSeriesEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestSeriesId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestSeriesId");
        entity.HasOne<SyncAnchorEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.SourceSyncAnchorId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_SourceSyncAnchorId");
        entity.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Skill");
        entity.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => row.ProfileRewardId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_ProfileReward");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Category");
    }
}
