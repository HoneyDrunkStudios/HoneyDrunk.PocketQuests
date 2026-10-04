using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestOccurrenceEvent ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questOccurrenceRevisionService">QuestOccurrenceRevision business behavior.</param>
public sealed class QuestOccurrenceEventService(IQuestOccurrenceEventDataService data, IQuestOccurrenceRevisionService questOccurrenceRevisionService) : IQuestOccurrenceEventService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestOccurrenceEventEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestOccurrenceEventEntity> SaveAsync(Guid accountId, QuestOccurrenceEventEntity value, CancellationToken cancellationToken = default)
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
            || original.QuestOccurrenceId != current.QuestOccurrenceId
            || original.QuestOccurrenceRevisionId != current.QuestOccurrenceRevisionId
            || original.EventCode != current.EventCode
            || original.EffectiveAt != current.EffectiveAt
            || original.AccountMutationVersion != current.AccountMutationVersion
            || original.CommandReceiptId != current.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestOccurrenceId != value.QuestOccurrenceId
            || current.QuestOccurrenceRevisionId != value.QuestOccurrenceRevisionId
            || current.EventCode != value.EventCode
            || current.EffectiveAt != value.EffectiveAt
            || current.AccountMutationVersion != value.AccountMutationVersion
            || current.CommandReceiptId != value.CommandReceiptId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task AppendAsync(QuestCommit change, Guid id, Guid occurrenceId, string code, DateTimeOffset at, CancellationToken token = default)
    {
        var account = change.Account;
        if ((await GetByAccountIdAsync(account.Id, token)).Any(row => row.Id == id))
            return;
        await SaveAsync(
            account.Id,
            new QuestOccurrenceEventEntity
            {
                Id = id,
                AccountId = account.Id,
                QuestOccurrenceId = occurrenceId,
                QuestOccurrenceRevisionId = await questOccurrenceRevisionService.CurrentRevisionIdAsync(account.Id, occurrenceId, token),
                EventCode = code,
                EffectiveAt = at,
                AccountMutationVersion = change.Version,
                CommandReceiptId = change.ReceiptId,
                CreatedAt = change.Now,
            },
            token);
    }
}
