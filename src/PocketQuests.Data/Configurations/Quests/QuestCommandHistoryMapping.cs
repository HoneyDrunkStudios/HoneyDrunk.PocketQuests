using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestCommandHistory fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestCommandHistoryMapping : IEntityTypeConfiguration<QuestCommandHistoryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestCommandHistoryEntity> builder)
    {
        builder.ToTable("QuestCommandHistory", "pocketquests", table =>
        {
            table.HasCheckConstraint("CK_QuestCommandHistory_AvatarAction", "([ActionCode]='save-avatar' AND [AvatarCode] IS NOT NULL) OR ([ActionCode]<>'save-avatar' AND [AvatarCode] IS NULL)");
            table.HasCheckConstraint("CK_QuestCommandHistory_AvatarCode", "[AvatarCode] IS NULL OR (DATALENGTH([AvatarCode])=12 AND [AvatarCode] LIKE 'v1:[0-5]:[0-3]:[0-3]:[0-3]:[0-3]') OR (DATALENGTH([AvatarCode])=16 AND [AvatarCode] LIKE 'v2:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]') OR (DATALENGTH([AvatarCode])=20 AND [AvatarCode] LIKE 'v3:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]:[0-2]:[0-7]')");
            table.HasComment("One row is one immutable, typed successful action input and its replay clocks, never a serialized command or account snapshot. Classification: Restricted. History: immutable. Erase with its account; no age cutoff on replay history.");
            table.HasCheckConstraint("CK_QuestCommandHistory_CharacterName", "[CharacterName] IS NULL OR (LEN(LTRIM(RTRIM([CharacterName]))) BETWEEN 1 AND 40 AND DATALENGTH([CharacterName])<=80)");
            table.HasCheckConstraint("CK_QuestCommandHistory_PortraitId", "[PortraitId] IS NULL OR [PortraitId] IN ('portrait-1','portrait-2','portrait-3','portrait-4','portrait-5','portrait-6')");
            table.HasCheckConstraint("CK_QuestCommandHistory_CharacterPair", "([CharacterName] IS NULL AND [PortraitId] IS NULL) OR ([CharacterName] IS NOT NULL AND [PortraitId] IS NOT NULL)");
            table.HasCheckConstraint("CK_QuestCommandHistory_CharacterAction", "([ActionCode]='save-character' AND [CharacterName] IS NOT NULL AND [PortraitId] IS NOT NULL) OR ([ActionCode]<>'save-character' AND [CharacterName] IS NULL AND [PortraitId] IS NULL)");
            table.HasCheckConstraint("CK_QuestCommandHistory_Action", "[ActionCode] IN ('accept','complete','undo','save-definition','archive-definition','assess-skill','save-skill','archive-skill','interests','finish-onboarding','save-character','save-avatar','plan','zone','expiry-warnings','link','select-badge','select-frame','save-series','stop-series','pause','resume','resume-occurrence','abandon','accept-offer','$reconcile','$lifecycle-pause')");
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
        builder.Property(row => row.AvatarCode).HasComment("Original validated avatar appearance for save-avatar; null for other commands. Immutable private account history, erased with its owner. Version v3 appends an extra starter accessory and scarf color; earlier versions retain their exact meanings.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(20).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasKey(row => row.Id).HasName("PK_QuestCommandHistory").IsClustered();
        builder.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_QuestCommandHistory_Account_Id");
        builder.HasIndex(row => new { row.AccountId, row.AccountMutationVersion }, "UQ_QuestCommandHistory_Account_Version").IsUnique().HasFilter(null);
        builder.Property(row => row.CharacterName).HasComment("Immutable submitted Player-chosen character display name, 1 to 40 UTF-16 code units; null means the player has not chosen a character. Restricted account data; erase with the account.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.PortraitId).HasComment("Immutable submitted Bundled portrait code portrait-1 through portrait-6; null means the player has not chosen a character. No remote URL or upload; erase with the account.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(16).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.Id).HasComment("Stable transition UUID, equal to the operation UUID for a client command.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.AccountId).HasComment("Owning personal account; all references use this same ownership boundary.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CommandReceiptId).HasComment("Receipt for a client command; null for an internal reconciliation or lifecycle transition. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.AccountMutationVersion).HasComment("Committed account sequence used to reconstruct a receipt or an issued sync anchor.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ActionCode).HasComment("Stable command action or the documented internal reconciliation/lifecycle action.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.RulesetVersion).HasComment("Retained domain replay implementation version; never silently replay through a different ruleset.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.TimeZoneBefore).HasComment("IANA zone immediately before this transition; the first row preserves the initial account zone.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ReconciledAt).HasComment("UTC clock through which due series deliveries were reconciled before applying the action.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.RecordedAt).HasComment("Verified UTC effective action time; may precede receipt by any duration.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ProjectionAt).HasComment("Original UTC response projection time, independent of later retries.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestOccurrenceId).HasComment("Target or server-resolved new occurrence identity. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestCompletionId).HasComment("Completion selected by Undo. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestDefinitionRevisionId).HasComment("Immutable quest terms supplied to a definition, acceptance, series or offer command. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CompletionTermsRevisionId).HasComment("Frozen occurrence revision actually used for a delayed completion after a later edit. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ExpectedRevision).HasComment("Optimistic public definition, skill or series revision supplied by the successful command. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.SystemSkillId).HasComment("System skill selected for assessment. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CustomSkillId).HasComment("Owned skill selected for creation, editing, archival or assessment. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.SkillName).HasComment("Validated name supplied by a skill save command; this is historical input, not the mutable current name. Null means not applicable to this transition.").HasMaxLength(80).IsUnicode(true).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ExperienceCode).HasComment("Validated experience selected for an assessment. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.DueOn).HasComment("Requested due or first-delivery local date. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.PlannedTime).HasColumnType("time(0)").HasComment("Requested local hour and minute. Null means not applicable to this transition.").HasPrecision(0).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ParentQuestOccurrenceId).HasComment("Owned parent selected by a link command. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ProfileRewardId).HasComment("Selected badge or frame; null also records an explicit clear. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestSeriesId).HasComment("Owned recurrence selected for creation, edit or stop. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CadenceCode).HasComment("Requested recurrence unit. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(6).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.Interval).HasComment("Requested positive recurrence interval. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CategoryId).HasComment("Category pause/resume target; null means the whole account. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ConfirmPenalty).HasComment("Whether the successful command explicitly confirmed penalty terms.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.AcceptedLoss).HasComment("Exact penalty amount accepted by the command. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.HasAcceptedTerms).HasComment("Whether the client supplied accepted quest terms; reconstruct them from the immutable typed terms reference.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.NewTimeZone).HasComment("Requested IANA zone for a zone-change command. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ExpectedTimeZone).HasComment("Optimistic previous IANA zone supplied by the command. Null means not applicable to this transition.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ConfirmZoneChange).HasComment("Whether a successful zone change was explicitly confirmed.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.HasExpiryWarnings).HasComment("Requested expiry-warning preference. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.SourceSyncAnchorId).HasComment("Original proof anchor, retaining offline acceptance provenance without copying an anchor snapshot. Null means not applicable to this transition.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CreatedAt).HasComment("UTC server insertion instant; never substituted for the effective action clock.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ReconciliationLimit).HasComment("Maximum due-delivery cursor steps performed before this action; zero records lifecycle transitions that intentionally do not reconcile before freezing. Retain this input for exact partial-batch replay.").HasDefaultValueSql("2147483647").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.ActionReconciliationLimit).HasComment("Maximum due-delivery cursor work performed by the action itself, such as a zone change or series creation; retained independently of the pre-action budget for exact original-response replay.").HasDefaultValueSql("2147483647").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(row => new { row.AccountId, row.CommandReceiptId }, "IX_QuestCommandHistory_CommandReceiptId");
        builder.HasIndex(row => new { row.AccountId, row.QuestOccurrenceId }, "IX_QuestCommandHistory_QuestOccurrenceId");
        builder.HasIndex(row => new { row.AccountId, row.QuestCompletionId }, "IX_QuestCommandHistory_QuestCompletionId");
        builder.HasIndex(row => new { row.AccountId, row.QuestDefinitionRevisionId }, "IX_QuestCommandHistory_QuestDefinitionRevisionId");
        builder.HasIndex(row => new { row.AccountId, row.CompletionTermsRevisionId }, "IX_QuestCommandHistory_CompletionTermsRevisionId");
        builder.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_QuestCommandHistory_CustomSkillId");
        builder.HasIndex(row => new { row.AccountId, row.ParentQuestOccurrenceId }, "IX_QuestCommandHistory_ParentQuestOccurrenceId");
        builder.HasIndex(row => new { row.AccountId, row.QuestSeriesId }, "IX_QuestCommandHistory_QuestSeriesId");
        builder.HasIndex(row => new { row.AccountId, row.SourceSyncAnchorId }, "IX_QuestCommandHistory_SourceSyncAnchorId");
        builder.HasIndex(row => row.SystemSkillId, "IX_QuestCommandHistory_SystemSkillId");
        builder.HasIndex(row => row.ProfileRewardId, "IX_QuestCommandHistory_ProfileRewardId");
        builder.HasIndex(row => row.CategoryId, "IX_QuestCommandHistory_CategoryId");
        builder.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Account");
        builder.HasOne<CommandReceiptEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CommandReceiptId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CommandReceiptId");
        builder.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestOccurrenceId");
        builder.HasOne<QuestCompletionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestCompletionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestCompletionId");
        builder.HasOne<QuestDefinitionRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestDefinitionRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestDefinitionRevisionId");
        builder.HasOne<QuestOccurrenceRevisionEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CompletionTermsRevisionId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CompletionTermsRevisionId");
        builder.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_CustomSkillId");
        builder.HasOne<QuestOccurrenceEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.ParentQuestOccurrenceId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_ParentQuestOccurrenceId");
        builder.HasOne<QuestSeriesEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestSeriesId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_QuestSeriesId");
        builder.HasOne<SyncAnchorEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.SourceSyncAnchorId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_SourceSyncAnchorId");
        builder.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Skill");
        builder.HasOne<ProfileRewardEntity>().WithMany().HasForeignKey(row => row.ProfileRewardId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_ProfileReward");
        builder.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandHistory_Category");
    }
}
