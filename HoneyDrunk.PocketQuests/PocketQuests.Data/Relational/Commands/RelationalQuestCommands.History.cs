using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using PocketQuests.Domain.Synchronization;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Retains successful typed inputs so historical profile, schedule and response values are reproducible.</summary>
public sealed partial class RelationalQuestCommands
{
    private static async Task<QuestAggregate> Load(Session session, AccountEntity account, long version, CancellationToken token)
    {
        if (version == account.MutationVersion && (version == 0 || await session.Context.Set<QuestCommandHistoryEntity>().AnyAsync(r => r.AccountId == account.Id, token)))
            return version == 0 ? new(account.TimeZoneId) : await LoadCurrent(session, account, token);
        return (await ReplayHistory(session, account, version, token)).Aggregate;
    }

    private static async Task<HistoryReplay> ReplayHistory(Session session, AccountEntity account, long version, CancellationToken token)
    {
        var db = session.Context;
        var history = await db.Set<QuestCommandHistoryEntity>().Where(r => r.AccountId == account.Id && r.AccountMutationVersion <= version)
            .OrderBy(r => r.AccountMutationVersion).ToListAsync(token);
        if (history.Count == 0)
        {
            var first = await db.Set<QuestCommandHistoryEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.AccountMutationVersion).FirstOrDefaultAsync(token);
            return first is not null && version == 0
                ? new(new QuestAggregate(first.TimeZoneBefore), null)
                : new(await LoadV1(session, account, version, token), null);
        }

        if (history[0].AccountMutationVersion != 1 || history.Count != version)
            throw new NotSupportedException("Incomplete or mixed history requires an explicit conversion before replay.");
        var terms = await ReadTerms(session, account.Id, token);
        var customKeys = await db.Set<CustomSkillEntity>().Where(r => r.AccountId == account.Id).ToDictionaryAsync(r => r.Id, r => r.ClientKey ?? r.Id.ToString(), token);
        var interests = await db.Set<QuestCommandInterestEntity>().Where(r => r.AccountId == account.Id).OrderBy(r => r.Position).ToListAsync(token);
        var revisions = await db.Set<QuestOccurrenceRevisionEntity>().Where(r => r.AccountId == account.Id).ToDictionaryAsync(r => r.Id, token);
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
                DateText(row.DueOn),
                row.QuestCompletionId,
                row.ActionCode == QuestActions.SaveDefinition ? quest : null,
                row.ExpectedRevision,
                row.SystemSkillId ?? (row.CustomSkillId is { } custom ? customKeys[custom] : null),
                row.ExperienceCode is null ? null : Enum.Parse<Experience>(row.ExperienceCode),
                row.ActionCode == QuestActions.Interests ? [.. interests.Where(i => i.QuestCommandHistoryId == row.Id).Select(i => i.CategoryId)] : null,
                TimeText(row.PlannedTime),
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
                aggregate.Occurrences[aggregate.Occurrences.IndexOf(current)] = HistoricalOccurrence(revisions[revisionId], terms, current);
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

    private static async Task<Dictionary<Guid, Quest>> ReadTerms(Session session, Guid accountId, CancellationToken token, Guid[]? selected = null)
    {
        var db = session.Context;
        var query = db.Set<QuestDefinitionRevisionEntity>().Where(r => r.AccountId == accountId);
        if (selected is not null)
            query = query.Where(r => selected.Contains(r.Id));
        var revisions = await query.ToListAsync(token);
        if (revisions.Count == 0)
            return [];
        var revisionIds = revisions.Select(r => r.Id).ToArray();
        var definitionIds = revisions.Select(r => r.QuestDefinitionId).Distinct().ToArray();
        var definitions = await db.Set<QuestDefinitionEntity>().Where(r => r.AccountId == accountId && definitionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
        var attributes = await db.Set<QuestDefinitionAttributeAllocationEntity>().Where(r => r.AccountId == accountId && revisionIds.Contains(r.QuestDefinitionRevisionId)).OrderBy(r => r.Position).ToListAsync(token);
        var skills = await db.Set<QuestDefinitionSkillAllocationEntity>().Where(r => r.AccountId == accountId && revisionIds.Contains(r.QuestDefinitionRevisionId)).OrderBy(r => r.Position).ToListAsync(token);
        var skillIds = skills.Where(r => r.CustomSkillId is not null).Select(r => r.CustomSkillId!.Value).Distinct().ToArray();
        var customKeys = await db.Set<CustomSkillEntity>().Where(r => r.AccountId == accountId && skillIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.ClientKey ?? r.Id.ToString(), token);
        var result = new Dictionary<Guid, Quest>();
        foreach (var row in revisions)
        {
            if (row.RulesetVersion != "1.0" || row.DisplaySnapshotVersion is not (1 or 2))
                throw new NotSupportedException("Historical quest terms require their retained ruleset/display implementation.");
            var identity = definitions[row.QuestDefinitionId];
            var attributePool = attributes.Where(a => a.QuestDefinitionRevisionId == row.Id).ToArray();
            var skillPool = skills.Where(s => s.QuestDefinitionRevisionId == row.Id).ToArray();
            ImmutableArray<Share> attributeShares = [.. attributePool.Select(a => new Share(a.AttributeId, a.BasisPoints))];
            ImmutableArray<Share> skillShares = [.. skillPool.Select(s => new Share(s.SystemSkillId ?? customKeys[s.CustomSkillId!.Value], s.BasisPoints))];
            if (row.DisplaySnapshotVersion == 1)
            {
                var display = JsonSerializer.Deserialize<FrozenDisplay>(row.DisplaySnapshotJson) ?? throw new InvalidOperationException("Missing historical display.");
                attributeShares = [.. display.AttributeOrder.Select(id => attributeShares.Single(a => a.Id == id))];
                skillShares = [.. display.SkillOrder.Select(id => skillShares.Single(s => s.Id == id))];
            }

            var quest = new Quest(identity.SystemQuestId ?? identity.ClientKey ?? identity.Id.ToString("D"), row.Title, row.Criterion, row.CategoryId, Enum.Parse<Rank>(row.RankCode), Enum.Parse<Effort>(row.EffortCode), attributeShares, skillShares, identity.SystemQuestId is null, row.Description, row.PenaltyPercent);
            if (quest.BaseXp != row.BaseXp)
                throw new NotSupportedException("Frozen XP rules do not match the retained domain implementation.");
            result.Add(row.Id, quest);
        }

        return result;
    }

    private static Occurrence HistoricalOccurrence(QuestOccurrenceRevisionEntity row, Dictionary<Guid, Quest> terms, Occurrence current) =>
        current with
        {
            Quest = terms[row.QuestDefinitionRevisionId],
            DueDate = DateText(row.DueOn),
            PlannedTime = TimeText(row.PlannedTime),
            Deadline = row.DeadlineAt is { } deadline ? NodaTime.Instant.FromDateTimeOffset(deadline).InZone(Scheduling.Zone(row.DeadlineTimeZoneId!)).ToDateTimeOffset() : null,
            Lifecycle = (current.Lifecycle ?? new()) with
            {
                FrozenAt = row.FrozenAt,
                IndividuallyFrozen = row.IsIndividuallyFrozen,
                AbandonedAt = row.AbandonedAt,
                LockedLoss = row.LockedLoss is { } loss ? checked((int)loss) : null,
                LossCategoryId = row.LossCategoryId,
                Unaccepted = row.AcceptedAt is null,
                DeadlineZone = row.DeadlineTimeZoneId,
            },
        };

    private static string? DateText(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? TimeText(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    private sealed record HistoryReplay(QuestAggregate Aggregate, CompletionOutcome? Outcome);
}
