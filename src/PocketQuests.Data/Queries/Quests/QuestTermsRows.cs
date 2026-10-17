using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Queries.Quests;

/// <summary>Owned immutable quest terms, allocations and stable client keys selected by the query.</summary>
public class QuestTermsRows
{
    /// <summary>Gets the owned CustomSkill rows.</summary>
    public required IReadOnlyList<CustomSkillEntity> Skills { get; init; }

    /// <summary>Gets the owned QuestDefinition rows.</summary>
    public required IReadOnlyList<QuestDefinitionEntity> Definitions { get; init; }

    /// <summary>Gets the owned QuestDefinitionRevision rows.</summary>
    public required IReadOnlyList<QuestDefinitionRevisionEntity> DefinitionRevisions { get; init; }

    /// <summary>Gets the owned QuestDefinitionAttributeAllocation rows.</summary>
    public required IReadOnlyList<QuestDefinitionAttributeAllocationEntity> Attributes { get; init; }

    /// <summary>Gets the owned QuestDefinitionSkillAllocation rows.</summary>
    public required IReadOnlyList<QuestDefinitionSkillAllocationEntity> SkillAllocations { get; init; }
}
