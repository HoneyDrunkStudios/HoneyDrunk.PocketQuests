using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class DefinitionMapping
{
    internal static QuestDefinition ToModel(this QuestDefinitionRevisionEntity row, Quest terms, bool archived) =>
        new(terms, row.Revision, archived);

    internal static QuestDefinitionEntity ToHead(this Quest quest, QuestMutation change, Guid id, int revision, int ordinal) => new()
    {
        Id = id,
        AccountId = change.Account.Id,
        SystemQuestId = quest.IsCustom ? null : quest.Id,
        ClientKey = quest.IsCustom ? quest.Id : null,
        Revision = revision,
        CreationOrdinal = ordinal,
    };

    internal static void ApplyTo(this QuestDefinition definition, QuestDefinitionEntity target)
    {
        target.Revision = definition.Revision;
    }

    internal static QuestDefinitionRevisionEntity ToRevision(this Quest quest, QuestMutation change, Guid definitionId, int revision) => new()
    {
        Id = QuestValues.Derived(change.Account.Id, $"definition/{definitionId:D}/revision/{revision}"),
        AccountId = change.Account.Id,
        QuestDefinitionId = definitionId,
        Revision = revision,
        Title = quest.Title,
        Criterion = quest.Criterion,
        Description = quest.Description,
        CategoryId = quest.CategoryId,
        RankCode = quest.Rank.ToString(),
        EffortCode = quest.Effort.ToString(),
        BaseXp = quest.BaseXp,
        PenaltyPercent = checked((byte)quest.PenaltyPercent),
        RulesetVersion = "1.0",
        DisplaySnapshotVersion = 2,
        DisplaySnapshotJson = JsonSerializer.Serialize(new { CategoryName = Catalog.Categories.Single(row => row.Id == quest.CategoryId).Name }),
        EffectiveAt = change.RecordedAt,
        CommandReceiptId = change.ReceiptId,
    };

    internal static IEnumerable<QuestDefinitionAttributeAllocationEntity> ToAttributes(this Quest quest, QuestMutation change, Guid revisionId) =>
        quest.Attributes.Select((allocation, index) => new QuestDefinitionAttributeAllocationEntity
        {
            AccountId = change.Account.Id,
            QuestDefinitionRevisionId = revisionId,
            AttributeId = allocation.Id,
            BasisPoints = allocation.BasisPoints,
            Position = index,
        });

    internal static IEnumerable<QuestDefinitionSkillAllocationEntity> ToSkills(this Quest quest, QuestMutation change, Guid revisionId) =>
        quest.Skills.Select((allocation, index) =>
        {
            var (system, custom) = QuestValues.Skill(allocation.Id);
            return new QuestDefinitionSkillAllocationEntity
            {
                Id = QuestValues.Derived(change.Account.Id, $"allocation/{revisionId:D}/{allocation.Id}"),
                AccountId = change.Account.Id,
                QuestDefinitionRevisionId = revisionId,
                SystemSkillId = system,
                CustomSkillId = custom,
                BasisPoints = allocation.BasisPoints,
                Position = index,
            };
        });
}
