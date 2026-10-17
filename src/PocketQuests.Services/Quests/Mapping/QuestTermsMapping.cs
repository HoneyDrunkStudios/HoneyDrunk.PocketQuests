using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Quests.Mapping;

internal static class QuestTermsMapping
{
    internal static Dictionary<Guid, Quest> ToTerms(this QuestTermsRows rows)
    {
        var definitions = rows.Definitions.ToDictionary(row => row.Id);
        var keys = rows.Skills.ToDictionary(row => row.Id, row => row.ClientKey ?? row.Id.ToString("D"));
        var attributes = rows.Attributes.ToLookup(row => row.QuestDefinitionRevisionId);
        var skills = rows.SkillAllocations.ToLookup(row => row.QuestDefinitionRevisionId);
        return rows.DefinitionRevisions.ToDictionary(row => row.Id, row => new Quest(
            definitions[row.QuestDefinitionId].SystemQuestId ?? definitions[row.QuestDefinitionId].ClientKey ?? row.QuestDefinitionId.ToString("D"),
            row.Title,
            row.Criterion,
            row.CategoryId,
            Enum.Parse<Rank>(row.RankCode),
            Enum.Parse<Effort>(row.EffortCode),
            [.. attributes[row.Id].Select(item => new Share(item.AttributeId, item.BasisPoints))],
            [.. skills[row.Id].Select(item => new Share(item.SystemSkillId ?? keys[item.CustomSkillId!.Value], item.BasisPoints))],
            definitions[row.QuestDefinitionId].SystemQuestId is null,
            row.Description,
            row.PenaltyPercent));
    }
}
