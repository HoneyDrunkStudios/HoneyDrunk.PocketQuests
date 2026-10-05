using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Quests.Mapping;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal sealed class QuestTermHistory(QuestMutation change, QuestTermsRows rows, QuestChanges changes)
{
    private readonly Dictionary<Guid, QuestDefinitionEntity> definitions = rows.Definitions.ToDictionary(row => row.Id);
    private readonly List<QuestDefinitionRevisionEntity> revisions = [.. rows.DefinitionRevisions];
    private readonly Dictionary<Guid, Quest> terms = QuestTerms.Read(rows);

    internal static Guid DefinitionId(Guid accountId, Quest quest) => quest.IsCustom ? Guid.Parse(quest.Id) : QuestValues.Derived(accountId, "definition/" + quest.Id);

    internal void ApplyDefinitions()
    {
        foreach (var definition in change.Aggregate.Definitions)
        {
            var revision = Ensure(definition.Quest, definition.Revision);
            var row = definitions[revision.QuestDefinitionId];
            if (row.Revision != definition.Revision || (row.ArchivedAt is not null) != definition.Archived)
                definition.ApplyTo(row, change);
        }
    }

    internal QuestDefinitionRevisionEntity Ensure(Quest quest, int? revision = null)
    {
        var definitionId = DefinitionId(change.Account.Id, quest);
        if (!definitions.TryGetValue(definitionId, out var identity))
        {
            var ordinal = quest.IsCustom ? change.Aggregate.Definitions.FindIndex(item => item.Quest.Id == quest.Id) + 1 : 0;
            identity = quest.ToHead(change, definitionId, revision ?? 1, ordinal);
            definitions.Add(identity.Id, identity);
            changes.Definitions.Add(identity);
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
        changes.DefinitionRevisions.Add(next);
        changes.DefinitionAttributes.AddRange(quest.ToAttributes(change, next.Id));
        changes.DefinitionSkills.AddRange(quest.ToSkills(change, next.Id));
        return next;
    }
}
