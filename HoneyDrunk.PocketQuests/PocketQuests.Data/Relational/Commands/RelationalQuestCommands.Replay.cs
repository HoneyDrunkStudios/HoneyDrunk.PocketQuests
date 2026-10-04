using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Reconstructs the supported domain state from immutable typed terms and visible events.</summary>
public sealed partial class RelationalQuestCommands
{
    private static async Task<QuestAggregate> LoadV1(Session session, AccountEntity account, long version, CancellationToken token)
    {
        var db = session.Context;

        // Fail closed instead of silently discarding data from command lanes not implemented yet.
        if (account.IsOnboardingComplete || account.HasExpiryWarnings || account.IsAccountPaused || account.SelectedBadgeId is not null || account.SelectedFrameId is not null
            || await db.Set<QuestSeriesEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<CustomSkillEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<SkillAssessmentEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<AccountPauseEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<AccountInterestEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<TimeZoneChangeEntity>().AnyAsync(r => r.AccountId == account.Id, token)
            || await db.Set<QuestDefinitionEntity>().AnyAsync(r => r.AccountId == account.Id && r.SystemQuestId == null, token))
            throw new NotSupportedException("This account requires additional relational replay lanes before cutover.");
        var revisions = await db.Set<QuestOccurrenceRevisionEntity>().Where(r => r.AccountId == account.Id && r.AccountMutationVersion <= version)
            .OrderBy(r => r.AccountMutationVersion).ThenBy(r => r.QuestOccurrenceId).ToListAsync(token);
        if (revisions.Any(r => r.Revision != 1 || r.FrozenAt is not null || r.AbandonedAt is not null || r.LockedLoss is not null || r.AcceptedAt is null))
            throw new NotSupportedException("Occurrence revision transitions require the next relational replay lane.");
        var occurrenceIds = revisions.Select(r => r.QuestOccurrenceId).ToArray();
        var current = await db.Set<QuestOccurrenceEntity>().Where(r => r.AccountId == account.Id && occurrenceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, token);
        if (current.Values.Any(r => r.ParentQuestOccurrenceId is not null || r.QuestSeriesId is not null))
            throw new NotSupportedException("Linked and recurring occurrences require their historical replay lane.");
        var definitionIds = revisions.Select(r => r.QuestDefinitionRevisionId).Distinct().ToArray();
        var definitions = await db.Set<QuestDefinitionRevisionEntity>().Where(r => r.AccountId == account.Id && definitionIds.Contains(r.Id)).ToListAsync(token);
        var identities = await db.Set<QuestDefinitionEntity>().Where(r => r.AccountId == account.Id).ToDictionaryAsync(r => r.Id, token);
        var attributes = await db.Set<QuestDefinitionAttributeAllocationEntity>().Where(r => r.AccountId == account.Id && definitionIds.Contains(r.QuestDefinitionRevisionId)).ToListAsync(token);
        var skills = await db.Set<QuestDefinitionSkillAllocationEntity>().Where(r => r.AccountId == account.Id && definitionIds.Contains(r.QuestDefinitionRevisionId)).ToListAsync(token);
        var quests = new Dictionary<Guid, Quest>();
        foreach (var definition in definitions)
        {
            if (definition.RulesetVersion != "1.0" || definition.DisplaySnapshotVersion != 1 || definition.PenaltyPercent != 0)
                throw new NotSupportedException("A retained ruleset/display replay implementation is required.");
            var display = JsonSerializer.Deserialize<FrozenDisplay>(definition.DisplaySnapshotJson) ?? throw new InvalidOperationException("Historical display is missing.");
            var attributePool = attributes.Where(a => a.QuestDefinitionRevisionId == definition.Id).ToDictionary(a => a.AttributeId, a => a.BasisPoints);
            var skillPool = skills.Where(s => s.QuestDefinitionRevisionId == definition.Id).ToDictionary(s => s.SystemSkillId!, s => s.BasisPoints);
            if (display.AttributeOrder.Length != attributePool.Count || display.SkillOrder.Length != skillPool.Count)
                throw new InvalidOperationException("Historical display and typed allocation membership disagree.");
            var quest = new Quest(
                identities[definition.QuestDefinitionId].SystemQuestId!,
                definition.Title,
                definition.Criterion,
                definition.CategoryId,
                Enum.Parse<Rank>(definition.RankCode),
                Enum.Parse<Effort>(definition.EffortCode),
                [.. display.AttributeOrder.Select(id => new Share(id, attributePool[id]))],
                [.. display.SkillOrder.Select(id => new Share(id, skillPool[id]))],
                Description: definition.Description);
            if (quest.BaseXp != definition.BaseXp)
                throw new NotSupportedException("The frozen reward ruleset must be replayed by its original implementation.");
            quests.Add(definition.Id, quest);
        }

        var occurrences = revisions.Select(r => new Occurrence(
            r.QuestOccurrenceId,
            quests[r.QuestDefinitionRevisionId],
            r.DueOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.DeadlineAt,
            r.AcceptedAt!.Value,
            r.PlannedTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Lifecycle: new(
                SourceAnchorId: current[r.QuestOccurrenceId].SourceSyncAnchorId,
                DeadlineZone: r.DeadlineTimeZoneId ?? account.TimeZoneId))).ToArray();
        var events = await db.Set<QuestOccurrenceEventEntity>().Where(e => e.AccountId == account.Id && e.AccountMutationVersion <= version).ToDictionaryAsync(e => e.Id, token);
        var completionRows = await db.Set<QuestCompletionEntity>().Where(c => c.AccountId == account.Id).ToListAsync(token);
        var revisionMap = revisions.ToDictionary(r => r.Id);
        var completions = completionRows.Where(c => events.ContainsKey(c.Id)).Select(c => new Completion(c.Id, c.QuestOccurrenceId, c.RecordedAt, quests[revisionMap[c.QuestOccurrenceRevisionId].QuestDefinitionRevisionId])).ToArray();

        // The mutable Undo annotation is visible only when its immutable event was committed by this version.
        var undos = completionRows.Where(c => c.UndoQuestOccurrenceEventId is { } id && events.ContainsKey(id))
            .Select(c => new UndoEvent(c.UndoQuestOccurrenceEventId!.Value, c.Id, c.UndoneAt!.Value)).ToArray();
        return new(account.TimeZoneId, occurrences, completions, undos);
    }

    // Presentation order/names only. Typed allocation rows remain the reward authority.
    private sealed record FrozenDisplay(ImmutableArray<string> AttributeOrder, ImmutableArray<string> SkillOrder,
        ImmutableDictionary<string, string> Names);
}
