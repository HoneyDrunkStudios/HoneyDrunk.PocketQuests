using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

// Historical term identity lookup. It neither stages writes nor duplicates EF tracking.
internal sealed class QuestTermHistory(Guid accountId, IReadOnlyList<QuestDefinitionRevisionEntity> revisions, IReadOnlyDictionary<Guid, Quest> terms)
{
    internal static Guid DefinitionId(Guid accountId, Quest quest) => quest.IsCustom ? Guid.Parse(quest.Id) : QuestValues.Derived(accountId, "definition/" + quest.Id);

    internal QuestDefinitionRevisionEntity Find(Quest quest)
    {
        var definitionId = DefinitionId(accountId, quest);
        var serialized = JsonSerializer.Serialize(quest);
        return revisions.Where(row => row.QuestDefinitionId == definitionId).OrderByDescending(row => row.Revision)
            .First(row => JsonSerializer.Serialize(terms[row.Id]) == serialized);
    }
}
