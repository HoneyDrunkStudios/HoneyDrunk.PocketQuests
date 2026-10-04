using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Models.Synchronization;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestCommandHistory ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questDefinitionService">QuestDefinition business behavior.</param>
/// <param name="questOccurrenceRevisionService">QuestOccurrenceRevision business behavior.</param>
/// <param name="questCommandInterestService">QuestCommandInterest business behavior.</param>
/// <param name="questDefinitionRevisionService">QuestDefinitionRevision business behavior.</param>
/// <param name="accountData">Owned relational source queries.</param>
public sealed class QuestCommandHistoryService(IQuestCommandHistoryDataService data, IAccountDataService accountData, IQuestDefinitionService questDefinitionService, IQuestOccurrenceRevisionService questOccurrenceRevisionService, IQuestCommandInterestService questCommandInterestService, IQuestDefinitionRevisionService questDefinitionRevisionService) : IQuestCommandHistoryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestCommandHistoryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestCommandHistoryEntity> SaveAsync(Guid accountId, QuestCommandHistoryEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(value.Id, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.Id != current.Id
            || original.AccountId != current.AccountId
            || original.CommandReceiptId != current.CommandReceiptId
            || original.AccountMutationVersion != current.AccountMutationVersion
            || original.ActionCode != current.ActionCode
            || original.RulesetVersion != current.RulesetVersion
            || original.TimeZoneBefore != current.TimeZoneBefore
            || original.ReconciledAt != current.ReconciledAt
            || original.RecordedAt != current.RecordedAt
            || original.ProjectionAt != current.ProjectionAt
            || original.QuestOccurrenceId != current.QuestOccurrenceId
            || original.QuestCompletionId != current.QuestCompletionId
            || original.QuestDefinitionRevisionId != current.QuestDefinitionRevisionId
            || original.CompletionTermsRevisionId != current.CompletionTermsRevisionId
            || original.ExpectedRevision != current.ExpectedRevision
            || original.SystemSkillId != current.SystemSkillId
            || original.CustomSkillId != current.CustomSkillId
            || original.SkillName != current.SkillName
            || original.ExperienceCode != current.ExperienceCode
            || original.DueOn != current.DueOn
            || original.PlannedTime != current.PlannedTime
            || original.ParentQuestOccurrenceId != current.ParentQuestOccurrenceId
            || original.ProfileRewardId != current.ProfileRewardId
            || original.QuestSeriesId != current.QuestSeriesId
            || original.CadenceCode != current.CadenceCode
            || original.Interval != current.Interval
            || original.CategoryId != current.CategoryId
            || original.ConfirmPenalty != current.ConfirmPenalty
            || original.AcceptedLoss != current.AcceptedLoss
            || original.HasAcceptedTerms != current.HasAcceptedTerms
            || original.NewTimeZone != current.NewTimeZone
            || original.ExpectedTimeZone != current.ExpectedTimeZone
            || original.ConfirmZoneChange != current.ConfirmZoneChange
            || original.HasExpiryWarnings != current.HasExpiryWarnings
            || original.SourceSyncAnchorId != current.SourceSyncAnchorId
            || original.ReconciliationLimit != current.ReconciliationLimit
            || original.ActionReconciliationLimit != current.ActionReconciliationLimit)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.CommandReceiptId != value.CommandReceiptId
            || current.AccountMutationVersion != value.AccountMutationVersion
            || current.ActionCode != value.ActionCode
            || current.RulesetVersion != value.RulesetVersion
            || current.TimeZoneBefore != value.TimeZoneBefore
            || current.ReconciledAt != value.ReconciledAt
            || current.RecordedAt != value.RecordedAt
            || current.ProjectionAt != value.ProjectionAt
            || current.QuestOccurrenceId != value.QuestOccurrenceId
            || current.QuestCompletionId != value.QuestCompletionId
            || current.QuestDefinitionRevisionId != value.QuestDefinitionRevisionId
            || current.CompletionTermsRevisionId != value.CompletionTermsRevisionId
            || current.ExpectedRevision != value.ExpectedRevision
            || current.SystemSkillId != value.SystemSkillId
            || current.CustomSkillId != value.CustomSkillId
            || current.SkillName != value.SkillName
            || current.ExperienceCode != value.ExperienceCode
            || current.DueOn != value.DueOn
            || current.PlannedTime != value.PlannedTime
            || current.ParentQuestOccurrenceId != value.ParentQuestOccurrenceId
            || current.ProfileRewardId != value.ProfileRewardId
            || current.QuestSeriesId != value.QuestSeriesId
            || current.CadenceCode != value.CadenceCode
            || current.Interval != value.Interval
            || current.CategoryId != value.CategoryId
            || current.ConfirmPenalty != value.ConfirmPenalty
            || current.AcceptedLoss != value.AcceptedLoss
            || current.HasAcceptedTerms != value.HasAcceptedTerms
            || current.NewTimeZone != value.NewTimeZone
            || current.ExpectedTimeZone != value.ExpectedTimeZone
            || current.ConfirmZoneChange != value.ConfirmZoneChange
            || current.HasExpiryWarnings != value.HasExpiryWarnings
            || current.SourceSyncAnchorId != value.SourceSyncAnchorId
            || current.ReconciliationLimit != value.ReconciliationLimit
            || current.ActionReconciliationLimit != value.ActionReconciliationLimit)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task AppendAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var reconciledAt = change.ReconciledAt;
        var projectionAt = change.ProjectionAt;
        var now = change.Now;

        var action = command.Action;
        var targetOccurrence = action is QuestActions.Accept ? aggregate.Occurrences[^1] : command.OccurrenceId is { } id ? aggregate.Occurrences.SingleOrDefault(o => o.Id == id) : null;
        Quest? quest = action switch
        {
            QuestActions.SaveDefinition => aggregate.Definitions.Single(d => d.Quest.Id == command.Definition!.Id).Quest,
            QuestActions.ArchiveDefinition => aggregate.Definitions.Single(d => d.Quest.Id == command.QuestId).Quest,
            QuestActions.SaveSeries => aggregate.Schedule.Series.Single(s => s.Id == command.SeriesId).Quest,
            QuestActions.Accept or QuestActions.AcceptOffer or QuestActions.Abandon => targetOccurrence?.Quest,
            _ => null,
        };
        var (system, custom) = action is QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.AssessSkill ? QuestValues.Skill(command.SkillId!) : default;
        var history = new QuestCommandHistoryEntity
        {
            Id = command.OperationId,
            AccountId = account.Id,
            CommandReceiptId = change.ReceiptId,
            AccountMutationVersion = change.Version,
            ReconciliationLimit = change.ReconciliationLimit,
            ActionReconciliationLimit = change.ActionReconciliationLimit,
            ActionCode = action,
            RulesetVersion = "1.0",
            TimeZoneBefore = change.TimeZoneBefore,
            ReconciledAt = reconciledAt,
            RecordedAt = recordedAt,
            ProjectionAt = projectionAt,
            QuestOccurrenceId = action is QuestActions.Accept or QuestActions.Complete or QuestActions.Undo or QuestActions.Plan or QuestActions.Link
                or QuestActions.ResumeOccurrence or QuestActions.Abandon or QuestActions.AcceptOffer ? targetOccurrence?.Id : null,
            QuestCompletionId = action == QuestActions.Undo ? command.CompletionId : null,
            QuestDefinitionRevisionId = quest is null ? null : await questDefinitionService.EnsureTermsAsync(change, quest, token: token),
            CompletionTermsRevisionId = action == QuestActions.Complete && command.RecordedTime is not null ? await questOccurrenceRevisionService.CurrentRevisionIdAsync(account.Id, targetOccurrence!.Id, token) : null,
            ExpectedRevision = action is QuestActions.SaveDefinition or QuestActions.ArchiveDefinition or QuestActions.SaveSkill or QuestActions.ArchiveSkill or QuestActions.SaveSeries ? command.ExpectedRevision : null,
            SystemSkillId = system,
            CustomSkillId = custom,
            SkillName = action == QuestActions.SaveSkill ? aggregate.Profile.CustomSkills!.Single(s => s.Id == command.SkillId).Name : null,
            ExperienceCode = action == QuestActions.AssessSkill ? command.Experience!.Value.ToString() : null,
            DueOn = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? QuestValues.Date(command.DueDate) : null,
            PlannedTime = action is QuestActions.Accept or QuestActions.Plan or QuestActions.SaveSeries ? QuestValues.Time(command.PlannedTime) : null,
            ParentQuestOccurrenceId = action == QuestActions.Link ? command.ParentId : null,
            ProfileRewardId = action is QuestActions.SelectBadge or QuestActions.SelectFrame ? command.RewardId : null,
            QuestSeriesId = action is QuestActions.SaveSeries or QuestActions.StopSeries ? command.SeriesId : null,
            CadenceCode = action == QuestActions.SaveSeries ? command.Cadence!.Value.ToString() : null,
            Interval = action == QuestActions.SaveSeries ? command.Interval : null,
            CategoryId = action is QuestActions.Pause or QuestActions.Resume ? command.CategoryId : null,
            ConfirmPenalty = command.ConfirmPenalty,
            AcceptedLoss = command.AcceptedLoss,
            HasAcceptedTerms = command.AcceptedQuest is not null && quest is not null,
            NewTimeZone = action == QuestActions.Zone ? command.NewZone : null,
            ExpectedTimeZone = action == QuestActions.Zone ? command.ExpectedZone : null,
            ConfirmZoneChange = command.ConfirmZoneChange,
            HasExpiryWarnings = action == QuestActions.ExpiryWarnings ? command.ExpiryWarnings : null,
            SourceSyncAnchorId = command.RecordedTime?.AnchorId,
            CreatedAt = now
        };
        await SaveAsync(account.Id, history, token);
        if (action == QuestActions.Interests)
        {
            for (var index = 0; index < aggregate.Profile.Interests.Length; index++)
            {
                await questCommandInterestService.SaveAsync(
                    account.Id,
                    new QuestCommandInterestEntity
                    {
                        AccountId = account.Id,
                        QuestCommandHistoryId = command.OperationId,
                        CategoryId = aggregate.Profile.Interests[index],
                        Position = index,
                        CreatedAt = now
                    },
                    token);
            }
        }
    }

    /// <inheritdoc />
    public async Task<QuestReplay> ReplayAsync(AccountEntity account, long version, CancellationToken token = default)
    {
        var rows = await accountData.GetQuestStateAsync(account.Id, token);
        var history = rows.QuestCommandHistoryRows.Where(r => r.AccountId == account.Id && r.AccountMutationVersion <= version)
            .OrderBy(r => r.AccountMutationVersion).ToList();
        if (history.Count == 0)
        {
            var first = rows.QuestCommandHistoryRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.AccountMutationVersion).FirstOrDefault();
            return first is not null && version == 0
                ? new(new QuestAggregate(first.TimeZoneBefore), null)
                : version == 0 ? new(new QuestAggregate(account.TimeZoneId), null) : throw new NotSupportedException("The requested historical version has no retained source history.");
        }

        if (history[0].AccountMutationVersion != 1 || history.Count != version)
            throw new NotSupportedException("Incomplete or mixed history requires an explicit conversion before replay.");
        var terms = await questDefinitionRevisionService.ReadTermsAsync(account.Id, token);
        var customKeys = rows.CustomSkillRows.Where(r => r.AccountId == account.Id).ToDictionary(r => r.Id, r => r.ClientKey ?? r.Id.ToString());
        var interests = rows.QuestCommandInterestRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.Position).ToList();
        var revisions = rows.QuestOccurrenceRevisionRows.Where(r => r.AccountId == account.Id).ToDictionary(r => r.Id);
        var aggregate = new QuestAggregate(history[0].TimeZoneBefore);
        CompletionOutcome? outcome = null;
        foreach (var row in history)
        {
            if (row.RulesetVersion != "1.0" || row.TimeZoneBefore != aggregate.Zone)
                throw new NotSupportedException("The original ruleset or coherent historical zone is required for replay.");
            if (row.ReconciliationLimit > 0)
                _ = aggregate.Reconcile(row.ReconciledAt, row.ReconciliationLimit);
            outcome = null;
            if (row.ActionCode == "$reconcile")
            {
                aggregate = RetainProjectedProfile(aggregate, aggregate.Project(row.ProjectionAt).Profile);
                continue;
            }

            if (row.ActionCode == "$lifecycle-pause")
            {
                _ = aggregate.Apply(new(row.Id, QuestActions.Pause), row.RecordedAt, row.ActionReconciliationLimit);
                aggregate = RetainProjectedProfile(aggregate, aggregate.Project(row.ProjectionAt).Profile);
                continue;
            }

            Quest? quest = row.QuestDefinitionRevisionId is { } termId ? terms[termId] : null;
            var command = new QuestCommand(
                row.Id,
                row.ActionCode,
                row.QuestOccurrenceId,
                row.ActionCode is QuestActions.Accept or QuestActions.SaveSeries or QuestActions.ArchiveDefinition ? quest?.Id : null,
                QuestValues.DateText(row.DueOn),
                row.QuestCompletionId,
                row.ActionCode == QuestActions.SaveDefinition ? quest : null,
                row.ExpectedRevision,
                row.SystemSkillId ?? (row.CustomSkillId is { } custom ? customKeys[custom] : null),
                row.ExperienceCode is null ? null : Enum.Parse<Experience>(row.ExperienceCode),
                row.ActionCode == QuestActions.Interests ? [.. interests.Where(i => i.QuestCommandHistoryId == row.Id).Select(i => i.CategoryId)] : null,
                QuestValues.TimeText(row.PlannedTime),
                row.ParentQuestOccurrenceId,
                row.ProfileRewardId,
                row.QuestSeriesId,
                row.CadenceCode is null ? null : Enum.Parse<Cadence>(row.CadenceCode),
                row.Interval,
                row.CategoryId,
                row.ConfirmPenalty,
                row.AcceptedLoss,
                row.SourceSyncAnchorId is { } anchor ? new RecordedActionTime(anchor, Guid.Empty, 0, 0, row.RecordedAt) : null,
                row.SkillName,
                row.HasAcceptedTerms ? quest : null,
                row.NewTimeZone,
                row.ExpectedTimeZone,
                row.ConfirmZoneChange,
                row.HasExpiryWarnings);
            if (row.CompletionTermsRevisionId is { } revisionId)
            {
                var current = aggregate.Occurrences.Single(o => o.Id == row.QuestOccurrenceId);
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(current)] = QuestOccurrenceRevisionService.ToModel(revisions[revisionId], terms, current);
            }

            var before = row.ActionCode == QuestActions.Complete ? aggregate.Project(row.ProjectionAt) : null;
            _ = aggregate.Apply(command, row.RecordedAt, row.ActionReconciliationLimit);
            var projected = aggregate.Project(row.ProjectionAt);
            if (before is not null)
                outcome = CompletionOutcome.Between(before, projected, row.Id, row.QuestOccurrenceId!.Value);

            // The existing store persists the projected profile. Undo can revoke an equipped
            // reward; earning it again does not silently reselect it on a later command/replay.
            aggregate = RetainProjectedProfile(aggregate, projected.Profile);
        }

        return new(aggregate, outcome);
    }

    private static QuestAggregate RetainProjectedProfile(QuestAggregate aggregate, PlayerProfile profile) =>
        aggregate.Profile.BadgeId == profile.BadgeId && aggregate.Profile.FrameId == profile.FrameId
            ? aggregate
            : new(aggregate.Zone, aggregate.Occurrences, aggregate.Completions, aggregate.Undos, aggregate.Definitions, profile, aggregate.Schedule);
}
