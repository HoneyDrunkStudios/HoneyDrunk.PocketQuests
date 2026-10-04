using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestDefinition ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questDefinitionRevisionService">QuestDefinitionRevision business behavior.</param>
/// <param name="questDefinitionAttributeAllocationService">QuestDefinitionAttributeAllocation business behavior.</param>
/// <param name="questDefinitionSkillAllocationService">QuestDefinitionSkillAllocation business behavior.</param>
/// <param name="historyData">Scoped IQuestCommandHistoryDataService query access.</param>
public sealed class QuestDefinitionService(IQuestDefinitionDataService data, IQuestDefinitionRevisionService questDefinitionRevisionService, IQuestDefinitionAttributeAllocationService questDefinitionAttributeAllocationService, IQuestDefinitionSkillAllocationService questDefinitionSkillAllocationService, IQuestCommandHistoryDataService historyData) : IQuestDefinitionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestDefinitionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestDefinitionEntity> SaveAsync(Guid accountId, QuestDefinitionEntity value, CancellationToken cancellationToken = default)
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
            || original.SystemQuestId != current.SystemQuestId
            || original.CreationOrdinal != current.CreationOrdinal
            || original.ClientKey != current.ClientKey)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.SystemQuestId != value.SystemQuestId
            || current.CreationOrdinal != value.CreationOrdinal
            || current.ClientKey != value.ClientKey)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.Revision = value.Revision;
        current.ArchivedAt = value.ArchivedAt;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task<Guid> EnsureTermsAsync(QuestCommit change, Quest quest, int? revision = null, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        var terms = await questDefinitionRevisionService.ReadTermsAsync(account.Id, token);
        var definitionId = quest.IsCustom ? Guid.Parse(quest.Id) : QuestValues.Derived(account.Id, "definition/" + quest.Id);
        var identity = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(d => d.Id == definitionId);
        if (identity is null)
        {
            identity = new()
            {
                Id = definitionId,
                AccountId = account.Id,
                SystemQuestId = quest.IsCustom ? null : quest.Id,
                ClientKey = quest.IsCustom ? quest.Id : null,
                Revision = revision ?? 1,
                CreationOrdinal = quest.IsCustom ? aggregate.Definitions.FindIndex(d => d.Quest.Id == quest.Id) + 1 : 0,
                CreatedAt = now,
                ModifiedAt = now
            };
            await SaveAsync(account.Id, identity, token);
        }

        var serialized = JsonSerializer.Serialize(quest);
        var candidates = (await questDefinitionRevisionService.GetByAccountIdAsync(account.Id, token)).Where(r => r.QuestDefinitionId == definitionId && (revision is null || r.Revision == revision))
            .OrderByDescending(r => r.Revision);
        foreach (var candidate in candidates)
        {
            if (terms.TryGetValue(candidate.Id, out var existing) && JsonSerializer.Serialize(existing) == serialized)
                return candidate.Id;
        }

        var number = revision ?? (await questDefinitionRevisionService.GetByAccountIdAsync(account.Id, token)).Where(r => r.QuestDefinitionId == definitionId).Select(r => r.Revision).DefaultIfEmpty(0).Max() + 1;
        var id = QuestValues.Derived(account.Id, $"definition/{definitionId:D}/revision/{number}");
        await questDefinitionRevisionService.SaveAsync(
            account.Id,
            new QuestDefinitionRevisionEntity
            {
                Id = id,
                AccountId = account.Id,
                QuestDefinitionId = definitionId,
                Revision = number,
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
                DisplaySnapshotJson = JsonSerializer.Serialize(new { CategoryName = Catalog.Categories.Single(c => c.Id == quest.CategoryId).Name }),
                EffectiveAt = recordedAt,
                CommandReceiptId = change.ReceiptId,
                CreatedAt = now,
            },
            token);
        for (var index = 0; index < quest.Attributes.Length; index++)
        {
            var allocation = quest.Attributes[index];
            await questDefinitionAttributeAllocationService.SaveAsync(
                account.Id,
                new QuestDefinitionAttributeAllocationEntity
                {
                    AccountId = account.Id,
                    QuestDefinitionRevisionId = id,
                    AttributeId = allocation.Id,
                    BasisPoints = allocation.BasisPoints,
                    Position = index,
                    CreatedAt = now
                },
                token);
        }

        for (var index = 0; index < quest.Skills.Length; index++)
        {
            var allocation = quest.Skills[index];
            var (system, custom) = QuestValues.Skill(allocation.Id);
            await questDefinitionSkillAllocationService.SaveAsync(
                account.Id,
                new QuestDefinitionSkillAllocationEntity
                {
                    Id = QuestValues.Derived(account.Id, $"allocation/{id:D}/{allocation.Id}"),
                    AccountId = account.Id,
                    QuestDefinitionRevisionId = id,
                    SystemSkillId = system,
                    CustomSkillId = custom,
                    BasisPoints = allocation.BasisPoints,
                    Position = index,
                    CreatedAt = now
                },
                token);
        }

        return id;
    }

    /// <inheritdoc />
    public async Task ApplyDefinitionsAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var recordedAt = change.RecordedAt;
        var now = change.Now;

        foreach (var definition in aggregate.Definitions)
        {
            _ = await EnsureTermsAsync(change, definition.Quest, definition.Revision, token);
            var row = (await GetByAccountIdAsync(account.Id, token)).Single(r => r.Id == Guid.Parse(definition.Quest.Id));
            row.Revision = definition.Revision;
            row.ArchivedAt = definition.Archived ? row.ArchivedAt ?? recordedAt : null;
            row.ModifiedAt = row.ModifiedAt > now ? row.ModifiedAt : now;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestDefinition>> ReadHistoryAsync(Guid accountId, CancellationToken token = default)
    {
        var definitions = (await data.GetByAccountIdAsync(accountId, token)).Where(row => row.SystemQuestId is null).Select(row => row.Id).ToHashSet();
        var revisions = (await questDefinitionRevisionService.GetByAccountIdAsync(accountId, token)).Where(row => definitions.Contains(row.QuestDefinitionId))
            .OrderBy(row => row.QuestDefinitionId).ThenBy(row => row.Revision).ToArray();
        var archived = (await historyData.GetByAccountIdAsync(accountId, token)).Where(row => row.ActionCode == "archive-definition").Select(row => row.QuestDefinitionRevisionId).ToHashSet();
        var terms = await questDefinitionRevisionService.ReadTermsAsync(accountId, token, revisions.Select(row => row.Id).ToArray());
        return revisions.Select(row => new QuestDefinition(terms[row.Id], row.Revision, archived.Contains(row.Id))).ToArray();
    }
}
