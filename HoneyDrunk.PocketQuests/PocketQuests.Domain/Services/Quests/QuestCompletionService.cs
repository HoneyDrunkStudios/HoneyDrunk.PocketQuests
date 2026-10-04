using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains QuestCompletion ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questOccurrenceRevisionService">QuestOccurrenceRevision business behavior.</param>
/// <param name="questOccurrenceEventService">QuestOccurrenceEvent business behavior.</param>
public sealed class QuestCompletionService(IQuestCompletionDataService data, IQuestOccurrenceRevisionService questOccurrenceRevisionService, IQuestOccurrenceEventService questOccurrenceEventService) : IQuestCompletionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<QuestCompletionEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<QuestCompletionEntity> SaveAsync(Guid accountId, QuestCompletionEntity value, CancellationToken cancellationToken = default)
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
            || original.RecordedAt != current.RecordedAt)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (original.UndoneAt is not null && (original.UndoneAt != current.UndoneAt || original.UndoQuestOccurrenceEventId != current.UndoQuestOccurrenceEventId))
            throw new InvalidOperationException("A committed Undo annotation cannot be replaced or removed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId
            || current.QuestOccurrenceId != value.QuestOccurrenceId
            || current.QuestOccurrenceRevisionId != value.QuestOccurrenceRevisionId
            || current.RecordedAt != value.RecordedAt)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.UndoneAt is not null && (current.UndoneAt != value.UndoneAt || current.UndoQuestOccurrenceEventId != value.UndoQuestOccurrenceEventId))
            throw new InvalidOperationException("A committed Undo annotation cannot be replaced or removed.");

        current.UndoneAt = value.UndoneAt;
        current.UndoQuestOccurrenceEventId = value.UndoQuestOccurrenceEventId;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task ApplyCompletionsAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var now = change.Now;

        foreach (var completion in aggregate.Completions)
        {
            var prior = (await GetByAccountIdAsync(account.Id, token)).SingleOrDefault(c => c.Id == completion.Id);
            if (prior is null)
            {
                await questOccurrenceEventService.AppendAsync(change, completion.Id, completion.OccurrenceId, "Completed", completion.RecordedAt, token);
                prior = new()
                {
                    Id = completion.Id,
                    AccountId = account.Id,
                    QuestOccurrenceId = completion.OccurrenceId,
                    QuestOccurrenceRevisionId = await questOccurrenceRevisionService.CurrentRevisionIdAsync(account.Id, completion.OccurrenceId, token),
                    RecordedAt = completion.RecordedAt,
                    CreatedAt = now,
                    ModifiedAt = now
                };
            }
            else
            {
                prior = new QuestCompletionEntity
                {
                    Id = prior.Id,
                    AccountId = prior.AccountId,
                    QuestOccurrenceId = prior.QuestOccurrenceId,
                    QuestOccurrenceRevisionId = prior.QuestOccurrenceRevisionId,
                    RecordedAt = prior.RecordedAt,
                    UndoneAt = prior.UndoneAt,
                    UndoQuestOccurrenceEventId = prior.UndoQuestOccurrenceEventId,
                    CreatedAt = prior.CreatedAt,
                    ModifiedAt = prior.ModifiedAt,
                    RowVersion = prior.RowVersion,
                };
            }

            var undo = aggregate.Undos.SingleOrDefault(u => u.CompletionId == completion.Id);
            if (undo is not null)
            {
                await questOccurrenceEventService.AppendAsync(change, undo.Id, completion.OccurrenceId, "Undone", undo.RecordedAt, token);
                prior.UndoneAt = undo.RecordedAt;
                prior.UndoQuestOccurrenceEventId = undo.Id;
                prior.ModifiedAt = now;
            }

            await SaveAsync(account.Id, prior, token);
        }
    }
}
