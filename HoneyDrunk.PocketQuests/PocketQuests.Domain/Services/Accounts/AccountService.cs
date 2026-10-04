using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Schedules;
using PocketQuests.Domain.Models.Skills;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Services.Quests;
using PocketQuests.Domain.Services.Skills;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Retains Account ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="accountInterestService">AccountInterest business behavior.</param>
/// <param name="accountPauseService">AccountPause business behavior.</param>
/// <param name="timeZoneChangeService">TimeZoneChange business behavior.</param>
/// <param name="customSkillService">CustomSkill business behavior.</param>
/// <param name="skillAssessmentService">SkillAssessment business behavior.</param>
/// <param name="questDefinitionRevisionService">QuestDefinitionRevision business behavior.</param>
public sealed class AccountService(IAccountDataService data, IAccountInterestService accountInterestService, IAccountPauseService accountPauseService, ITimeZoneChangeService timeZoneChangeService, ICustomSkillService customSkillService, ISkillAssessmentService skillAssessmentService, IQuestDefinitionRevisionService questDefinitionRevisionService) : IAccountService
{
    /// <inheritdoc />
    public Task<AccountEntity?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken = default) =>
        data.GetByIdentityUserIdAsync(identityUserId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountEntity> SaveAsync(AccountEntity value, CancellationToken cancellationToken = default)
    {
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
            || original.IdentityUserId != current.IdentityUserId
            || original.SelectedBadgeKind != current.SelectedBadgeKind
            || original.SelectedFrameKind != current.SelectedFrameKind)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.MutationVersion < original.MutationVersion || current.LastRecordedAt < original.LastRecordedAt)
            throw new InvalidOperationException("Committed account version and clock cannot move backwards.");
        if (current.Id != value.Id
            || current.IdentityUserId != value.IdentityUserId
            || current.SelectedBadgeKind != value.SelectedBadgeKind
            || current.SelectedFrameKind != value.SelectedFrameKind)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (value.MutationVersion < current.MutationVersion || value.LastRecordedAt < current.LastRecordedAt)
            throw new InvalidOperationException("Committed account version and clock cannot move backwards.");

        current.TimeZoneId = value.TimeZoneId;
        current.IsOnboardingComplete = value.IsOnboardingComplete;
        current.HasExpiryWarnings = value.HasExpiryWarnings;
        current.IsAccountPaused = value.IsAccountPaused;
        current.SelectedBadgeId = value.SelectedBadgeId;
        current.SelectedFrameId = value.SelectedFrameId;
        current.LastRecordedAt = value.LastRecordedAt;
        current.MutationVersion = value.MutationVersion;
        current.ProjectionVersion = value.ProjectionVersion;
        current.ProjectionAsOfAt = value.ProjectionAsOfAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        current.HasPendingReconciliation = value.HasPendingReconciliation;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplyProfileAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var command = change.Command;
        var aggregate = change.Aggregate;
        var state = change.State;
        var recordedAt = change.RecordedAt;
        var projectionAt = change.ProjectionAt;
        var now = change.Now;

        var profile = aggregate.Profile;
        var row = account;
        row.TimeZoneId = aggregate.Zone;
        row.IsOnboardingComplete = profile.OnboardingComplete;
        row.HasExpiryWarnings = profile.ExpiryWarnings;
        row.IsAccountPaused = aggregate.Schedule.AccountPaused;
        row.SelectedBadgeId = state.Profile.BadgeId;
        row.SelectedFrameId = state.Profile.FrameId;
        row.MutationVersion = change.Version;
        row.ProjectionVersion = change.Version;
        row.ProjectionAsOfAt = change.HasPending ? (account.ProjectionAsOfAt < recordedAt ? account.ProjectionAsOfAt : recordedAt) : projectionAt;
        row.HasPendingReconciliation = change.HasPending;
        row.LastRecordedAt = projectionAt;
        row.ModifiedAt = row.ModifiedAt > now ? row.ModifiedAt : now;

        await SaveAsync(row, token);
        await accountInterestService.ApplyInterestsAsync(change, token);
        await customSkillService.ApplySkillsAsync(change, token);

        await skillAssessmentService.RecordAssessmentAsync(change, token);

        await timeZoneChangeService.RecordChangeAsync(change, token);

        await accountPauseService.ApplyPausesAsync(change, token);
    }

    /// <inheritdoc />
    public async Task<QuestAggregate> LoadCurrentAsync(AccountEntity account, CancellationToken token = default)
    {
        var rows = await data.GetQuestStateAsync(account.Id, token);
        var terms = await questDefinitionRevisionService.ReadTermsAsync(account.Id, token);
        var definitions = rows.QuestDefinitionRows.Where(r => r.AccountId == account.Id && r.SystemQuestId == null).OrderBy(r => r.CreationOrdinal).ToList();
        var definitionRevisions = rows.QuestDefinitionRevisionRows.Where(r => r.AccountId == account.Id).ToList();
        var skills = rows.CustomSkillRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToList();
        var receipts = rows.CommandReceiptRows.Where(r => r.AccountId == account.Id).Select(r => new { r.Id, r.AppliedMutationVersion }).ToDictionary(r => r.Id, r => r.AppliedMutationVersion);
        var assessments = rows.SkillAssessmentRows.Where(r => r.AccountId == account.Id).ToList().OrderBy(r => receipts[r.CommandReceiptId]).ToArray();
        var zones = rows.TimeZoneChangeRows.Where(r => r.AccountId == account.Id).ToList().OrderBy(r => receipts[r.CommandReceiptId]).ToArray();
        var interests = rows.AccountInterestRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.Position).Select(r => r.CategoryId).ToList();
        var assessmentHistory = assessments.Select(r => new SkillAssessment(r.SystemSkillId ?? SkillKey(r.CustomSkillId!.Value), Enum.Parse<Experience>(r.ExperienceCode), r.EffectiveAt)).ToImmutableList();
        var assessed = ImmutableDictionary<string, Experience>.Empty;
        foreach (var assessment in assessmentHistory)
            assessed = assessed.SetItem(assessment.SkillId, assessment.Experience);
        var profile = new PlayerProfile(
            [.. interests],
            assessed,
            account.IsOnboardingComplete,
            account.SelectedBadgeId,
            account.SelectedFrameId,
            skills.Count == 0 ? null : [.. skills.Select(r => new CustomSkill(r.ClientKey ?? r.Id.ToString("D"), r.Name, r.Revision, r.ArchivedAt is not null))],
            assessmentHistory.Count == 0 ? null : assessmentHistory,
            zones.Length == 0 ? null : [.. zones.Select(r => new ZoneChange(r.FromTimeZoneId, r.ToTimeZoneId, r.EffectiveAt))],
            account.HasExpiryWarnings);
        var seriesRows = rows.QuestSeriesRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToList();
        var seriesRevisions = rows.QuestSeriesRevisionRows.Where(r => r.AccountId == account.Id).ToDictionary(r => r.Id);
        var series = seriesRows.Select(r =>
        {
            var revision = seriesRevisions.Values.Single(v => v.QuestSeriesId == r.Id && v.Revision == r.Revision);
            return new QuestSeries(r.Id, terms[revision.QuestDefinitionRevisionId], QuestValues.DateText(revision.AnchorOn)!, Enum.Parse<Cadence>(revision.CadenceCode), revision.Interval, revision.ScheduleVersion, r.NextSequence, r.PauseDays, r.StoppedAt is not null, QuestValues.TimeText(revision.PlannedTime), revision.HasAutoAcceptPenalty, revision.EffectiveAt);
        }).ToImmutableArray();
        var pauseRows = rows.AccountPauseRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToList();
        if (pauseRows.Any(r => r.ScopeCode != "Category" || r.CategoryId is null))
            throw new NotSupportedException("Effective pause history must use the domain's category union intervals.");
        var paused = rows.CategoryProgressRows.Where(r => r.AccountId == account.Id && r.IsExplicitlyPaused).Select(r => r.CategoryId).ToList();

        // The explicit category array is ordered by the successful pause/resume inputs. It is small
        // preference history only; loading it never replays quest, completion or reward commands.
        var pauseInputs = rows.QuestCommandHistoryRows.Where(r => r.AccountId == account.Id && (r.ActionCode == "pause" || r.ActionCode == "resume") && r.CategoryId != null)
            .OrderBy(r => r.AccountMutationVersion).Select(r => new { r.ActionCode, r.CategoryId }).ToList();
        var pauseOrder = new List<string>();
        foreach (var input in pauseInputs)
        {
            if (input.ActionCode == "resume")
                pauseOrder.Remove(input.CategoryId!);
            else if (!pauseOrder.Contains(input.CategoryId!))
                pauseOrder.Add(input.CategoryId!);
        }

        if (!pauseOrder.Order().SequenceEqual(paused.Order()))
            throw new InvalidOperationException("Explicit pause projection differs from retained preference history.");
        var schedule = new ScheduleState(series, [.. pauseRows.Select(r => new PauseWindow(r.CategoryId!, r.StartedAt, r.EndedAt))], [.. pauseOrder], account.IsAccountPaused);
        var occurrenceRows = rows.QuestOccurrenceRows.Where(r => r.AccountId == account.Id).OrderBy(r => r.CreationOrdinal).ToList();
        var occurrences = occurrenceRows.Select(r => QuestOccurrenceService.ToModel(r, terms, seriesRevisions)).ToArray();
        var occurrenceRevisions = rows.QuestOccurrenceRevisionRows.Where(r => r.AccountId == account.Id).ToDictionary(r => r.Id);
        var events = rows.QuestOccurrenceEventRows.Where(r => r.AccountId == account.Id && (r.EventCode == "Completed" || r.EventCode == "Undone")).ToDictionary(r => r.Id);
        var completionRows = rows.QuestCompletionRows.Where(r => r.AccountId == account.Id).ToList();
        var completions = completionRows.OrderBy(r => events[r.Id].AccountMutationVersion).Select(r => new Completion(r.Id, r.QuestOccurrenceId, r.RecordedAt, terms[occurrenceRevisions[r.QuestOccurrenceRevisionId].QuestDefinitionRevisionId])).ToArray();
        var undos = completionRows.Where(r => r.UndoQuestOccurrenceEventId is not null).OrderBy(r => events[r.UndoQuestOccurrenceEventId!.Value].AccountMutationVersion)
            .Select(r => new UndoEvent(r.UndoQuestOccurrenceEventId!.Value, r.Id, r.UndoneAt!.Value)).ToArray();
        var currentDefinitions = definitions.Select(r => new QuestDefinition(terms[definitionRevisions.Single(v => v.QuestDefinitionId == r.Id && v.Revision == r.Revision).Id], r.Revision, r.ArchivedAt is not null));
        return new(account.TimeZoneId, occurrences, completions, undos, currentDefinitions, profile, schedule);

        string SkillKey(Guid id)
        {
            var skill = skills.Single(s => s.Id == id);
            return skill.ClientKey ?? id.ToString("D");
        }
    }
}
