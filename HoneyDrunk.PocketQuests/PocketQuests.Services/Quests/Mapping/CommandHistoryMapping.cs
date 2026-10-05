using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CommandHistoryMapping
{
    internal static CommandReceiptEntity ToReceipt(QuestMutation change, byte[] digest) => new CommandReceiptEntity
    {
        Id = change.Command.OperationId,
        AccountId = change.Account.Id,
        CommandType = change.Command.Action,
        ApiVersion = 1,
        DigestVersion = 1,
        PayloadDigest = digest,
        OutcomeVersion = 2,
        OutcomeJson = JsonSerializer.Serialize(new CompactOutcome(change.ProjectionAt, null)),
        AppliedMutationVersion = change.Version,
        CreatedAt = change.Now,
    };

    internal static QuestCommandHistoryEntity ToHistory(QuestMutation change) => new QuestCommandHistoryEntity
    {
        Id = change.Command.OperationId,
        AccountId = change.Account.Id,
        CommandReceiptId = change.ReceiptId,
        AccountMutationVersion = change.Version,
        ReconciliationLimit = change.ReconciliationLimit,
        ActionReconciliationLimit = change.ActionReconciliationLimit,
        ActionCode = change.Command.Action,
        RulesetVersion = "1.0",
        TimeZoneBefore = change.TimeZoneBefore,
        ReconciledAt = change.ReconciledAt,
        RecordedAt = change.RecordedAt,
        ProjectionAt = change.ProjectionAt,
        QuestOccurrenceId = change.IsInternal ? null : change.Command.OccurrenceId,
        ConfirmPenalty = change.Command.ConfirmPenalty,
        AcceptedLoss = change.Command.AcceptedLoss,
        SourceSyncAnchorId = change.Command.RecordedTime?.AnchorId,
        CreatedAt = change.Now,
    };

    internal static AccountAuditRecordEntity ToOwnership(QuestMutation change, string auditId) =>
        new() { AccountId = change.Account.Id, AuditRecordId = auditId, CreatedAt = change.Now };

    internal static void ApplyToHistory(this QuestMutation change, QuestCommandHistoryEntity target, Occurrence? targetOccurrence, Quest? quest, Guid? termId, Guid? completionRevision, string? skillName)
    {
        var command = change.Command;
        var action = command.Action;
        var now = change.Now;
        var (system, custom) = action is QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.AssessSkill ? QuestValues.Skill(command.SkillId!) : default;
        target.QuestOccurrenceId = action is QuestActions.Accept or QuestActions.Complete or QuestActions.Undo or QuestActions.Plan or QuestActions.Link
        or QuestActions.ResumeOccurrence or QuestActions.Abandon or QuestActions.AcceptOffer ? targetOccurrence?.Id : null;
        target.QuestCompletionId = action == QuestActions.Undo ? command.CompletionId : null;
        target.QuestDefinitionRevisionId = quest is null ? null : termId;
        target.CompletionTermsRevisionId = action == QuestActions.Complete && command.RecordedTime is not null ? completionRevision : null;
        target.ExpectedRevision = action is QuestActions.SaveDefinition or QuestActions.ArchiveDefinition or QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.SaveSeries ? command.ExpectedRevision : null;
        target.SystemSkillId = system;
        target.CustomSkillId = custom;
        target.SkillName = skillName;
        target.ExperienceCode = action == QuestActions.AssessSkill ? command.Experience!.Value.ToString() : null;
        target.DueOn = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? QuestValues.Date(command.DueDate) : null;
        target.PlannedTime = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? QuestValues.Time(command.PlannedTime) : null;
        target.ParentQuestOccurrenceId = action == QuestActions.Link ? command.ParentId : null;
        target.ProfileRewardId = action is QuestActions.SelectBadge or QuestActions.SelectFrame ? command.RewardId : null;
        target.QuestSeriesId = action is QuestActions.SaveSeries or QuestActions.StopSeries ? command.SeriesId : null;
        target.CadenceCode = action == QuestActions.SaveSeries ? command.Cadence!.Value.ToString() : null;
        target.Interval = action == QuestActions.SaveSeries ? command.Interval : null;
        target.CategoryId = action is QuestActions.Pause or QuestActions.Resume ? command.CategoryId : null;
        target.ConfirmPenalty = command.ConfirmPenalty;
        target.AcceptedLoss = command.AcceptedLoss;
        target.HasAcceptedTerms = command.AcceptedQuest is not null && quest is not null;
        target.NewTimeZone = action == QuestActions.Zone ? command.NewZone : null;
        target.ExpectedTimeZone = action == QuestActions.Zone ? command.ExpectedZone : null;
        target.ConfirmZoneChange = command.ConfirmZoneChange;
        target.HasExpiryWarnings = action == QuestActions.ExpiryWarnings ? command.ExpiryWarnings : null;
        target.SourceSyncAnchorId = command.RecordedTime?.AnchorId;
        target.CreatedAt = now;
    }
}
