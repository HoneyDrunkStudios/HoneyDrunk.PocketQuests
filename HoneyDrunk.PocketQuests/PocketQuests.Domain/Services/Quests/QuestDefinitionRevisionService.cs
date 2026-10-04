using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestDefinitionRevision ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="definitions">Definition identities.</param>
/// <param name="attributes">Frozen attribute allocations.</param>
/// <param name="skills">Frozen skill allocations.</param>
/// <param name="customSkills">Stable custom skill identities.</param>
public sealed class QuestDefinitionRevisionService(IQuestDefinitionRevisionDataService data,
    IQuestDefinitionDataService definitions, IQuestDefinitionAttributeAllocationDataService attributes,
    IQuestDefinitionSkillAllocationDataService skills, ICustomSkillDataService customSkills) : IQuestDefinitionRevisionService
{
    /// <inheritdoc />
    public async Task<Dictionary<Guid, Quest>> ReadTermsAsync(Guid accountId, CancellationToken token = default, Guid[]? selected = null)
    {
        var revisions = (selected is null ? await data.GetByAccountIdAsync(accountId, token) : await data.GetSelectedAsync(accountId, selected, token)).ToArray();
        if (revisions.Length == 0)
            return [];
        var identities = (selected is null ? await definitions.GetByAccountIdAsync(accountId, token)
            : await definitions.GetSelectedAsync(accountId, revisions.Select(row => row.QuestDefinitionId).Distinct().ToArray(), token)).ToDictionary(row => row.Id);
        var attributeRows = selected is null ? await attributes.GetByAccountIdAsync(accountId, token) : await attributes.GetSelectedAsync(accountId, selected, token);
        var skillRows = selected is null ? await skills.GetByAccountIdAsync(accountId, token) : await skills.GetSelectedAsync(accountId, selected, token);
        var customKeys = (selected is null ? await customSkills.GetByAccountIdAsync(accountId, token)
            : await customSkills.GetSelectedAsync(accountId, skillRows.Where(row => row.CustomSkillId is not null).Select(row => row.CustomSkillId!.Value).Distinct().ToArray(), token)).ToDictionary(row => row.Id, row => row.ClientKey ?? row.Id.ToString("D"));
        var result = new Dictionary<Guid, Quest>();
        foreach (var row in revisions)
        {
            if (row.RulesetVersion != "1.0" || row.DisplaySnapshotVersion != 2)
                throw new NotSupportedException("Historical quest terms require their retained ruleset and display version.");
            var identity = identities[row.QuestDefinitionId];
            var quest = new Quest(
                identity.SystemQuestId ?? identity.ClientKey ?? identity.Id.ToString("D"),
                row.Title,
                row.Criterion,
                row.CategoryId,
                Enum.Parse<Rank>(row.RankCode),
                Enum.Parse<Effort>(row.EffortCode),
                [.. attributeRows.Where(item => item.QuestDefinitionRevisionId == row.Id).OrderBy(item => item.Position).Select(item => new Share(item.AttributeId, item.BasisPoints))],
                [.. skillRows.Where(item => item.QuestDefinitionRevisionId == row.Id).OrderBy(item => item.Position).Select(item => new Share(item.SystemSkillId ?? customKeys[item.CustomSkillId!.Value], item.BasisPoints))],
                identity.SystemQuestId is null,
                row.Description,
                row.PenaltyPercent);
            if (quest.BaseXp != row.BaseXp)
                throw new NotSupportedException("Frozen XP rules do not match the retained domain implementation.");
            result.Add(row.Id, quest);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<QuestDefinitionRevisionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestDefinitionRevisionEntity> SaveAsync(Guid accountId, QuestDefinitionRevisionEntity value, CancellationToken cancellationToken = default)
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
            || original.QuestDefinitionId != current.QuestDefinitionId
            || original.Revision != current.Revision
            || original.Title != current.Title
            || original.Criterion != current.Criterion
            || original.Description != current.Description
            || original.CategoryId != current.CategoryId
            || original.RankCode != current.RankCode
            || original.EffortCode != current.EffortCode
            || original.BaseXp != current.BaseXp
            || original.PenaltyPercent != current.PenaltyPercent
            || original.RulesetVersion != current.RulesetVersion
            || original.DisplaySnapshotVersion != current.DisplaySnapshotVersion
            || original.DisplaySnapshotJson != current.DisplaySnapshotJson
            || original.EffectiveAt != current.EffectiveAt
            || original.CommandReceiptId != current.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestDefinitionId != value.QuestDefinitionId
            || current.Revision != value.Revision
            || current.Title != value.Title
            || current.Criterion != value.Criterion
            || current.Description != value.Description
            || current.CategoryId != value.CategoryId
            || current.RankCode != value.RankCode
            || current.EffortCode != value.EffortCode
            || current.BaseXp != value.BaseXp
            || current.PenaltyPercent != value.PenaltyPercent
            || current.RulesetVersion != value.RulesetVersion
            || current.DisplaySnapshotVersion != value.DisplaySnapshotVersion
            || current.DisplaySnapshotJson != value.DisplaySnapshotJson
            || current.EffectiveAt != value.EffectiveAt
            || current.CommandReceiptId != value.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }
}
