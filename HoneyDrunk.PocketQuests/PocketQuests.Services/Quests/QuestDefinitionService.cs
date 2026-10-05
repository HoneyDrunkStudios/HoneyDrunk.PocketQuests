using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Quests.Mapping;
using System.Text.Json;

namespace PocketQuests.Services.Quests;

internal sealed class QuestDefinitionService(
    IQuestDefinitionDataService definitionData, IQuestDefinitionRevisionDataService revisionData,
    IQuestDefinitionAttributeAllocationDataService attributeData, IQuestDefinitionSkillAllocationDataService skillData)
{
    internal async Task<QuestTermHistory> Record(QuestMutation change, QuestTermsRows rows, CancellationToken token)
    {
        var definitions = rows.Definitions.ToDictionary(row => row.Id);
        List<QuestDefinitionRevisionEntity> revisions = [.. rows.DefinitionRevisions];
        var terms = QuestTerms.Read(rows);
        List<QuestDefinitionEntity> newDefinitions = [];
        List<QuestDefinitionRevisionEntity> newDefinitionRevisions = [];
        List<QuestDefinitionAttributeAllocationEntity> newDefinitionAttributes = [];
        List<QuestDefinitionSkillAllocationEntity> newDefinitionSkills = [];
        foreach (var definition in change.Aggregate.Definitions)
        {
            var revision = Ensure(definition.Quest, definition.Revision);
            var row = definitions[revision.QuestDefinitionId];
            if (row.Revision != definition.Revision || (row.ArchivedAt is not null) != definition.Archived)
            {
                definition.ApplyTo(row);
                row.ArchivedAt = definition.Archived ? row.ArchivedAt ?? change.RecordedAt : null;
                row.ModifiedAt = QuestClock.Max(row.ModifiedAt, change.Now);
            }
        }

        foreach (var series in change.Aggregate.Schedule.Series)
            Ensure(series.Quest);
        foreach (var view in change.State.Occurrences)
            Ensure(view.Occurrence.Quest);
        if (QuestHistory.CommandTargets(change).quest is { } selectedQuest)
            Ensure(selectedQuest);

        foreach (var row in newDefinitions)
            row.CreatedAt = row.ModifiedAt = change.Now;
        foreach (var row in newDefinitionRevisions)
            row.CreatedAt = change.Now;
        foreach (var row in newDefinitionAttributes)
            row.CreatedAt = change.Now;
        foreach (var row in newDefinitionSkills)
            row.CreatedAt = change.Now;

        await definitionData.AddRangeAsync(newDefinitions, token);
        await revisionData.AddRangeAsync(newDefinitionRevisions, token);
        await attributeData.AddRangeAsync(newDefinitionAttributes, token);
        await skillData.AddRangeAsync(newDefinitionSkills, token);
        return new(change.Account.Id, revisions, terms);

        QuestDefinitionRevisionEntity Ensure(Quest quest, int? revision = null)
        {
            var definitionId = QuestTermHistory.DefinitionId(change.Account.Id, quest);
            if (!definitions.TryGetValue(definitionId, out var identity))
            {
                var ordinal = quest.IsCustom ? change.Aggregate.Definitions.FindIndex(item => item.Quest.Id == quest.Id) + 1 : 0;
                identity = quest.ToHead(change, definitionId, revision ?? 1, ordinal);
                definitions.Add(identity.Id, identity);
                newDefinitions.Add(identity);
            }

            var serialized = JsonSerializer.Serialize(quest);
            var candidates = revisions.Where(row => row.QuestDefinitionId == definitionId && (revision is null || row.Revision == revision)).OrderByDescending(row => row.Revision);
            var existing = candidates.FirstOrDefault(row => JsonSerializer.Serialize(terms[row.Id]) == serialized);
            if (existing is not null)
                return existing;
            var number = revision ?? checked(revisions.Where(row => row.QuestDefinitionId == definitionId).Select(row => row.Revision).DefaultIfEmpty(0).Max() + 1);
            var next = quest.ToRevision(change, definitionId, number);
            revisions.Add(next);
            terms.Add(next.Id, quest);
            newDefinitionRevisions.Add(next);
            newDefinitionAttributes.AddRange(quest.ToAttributes(change, next.Id));
            newDefinitionSkills.AddRange(quest.ToSkills(change, next.Id));
            return next;
        }
    }
}
