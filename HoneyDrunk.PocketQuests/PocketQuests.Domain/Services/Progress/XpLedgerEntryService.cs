using PocketQuests.Data.DataServices.Progress;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Progress;

/// <summary>Retains XpLedgerEntry ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
/// <param name="questOccurrenceEventService">QuestOccurrenceEvent business behavior.</param>
public sealed class XpLedgerEntryService(IXpLedgerEntryDataService data, IQuestOccurrenceEventService questOccurrenceEventService) : IXpLedgerEntryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<XpLedgerEntryEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<XpLedgerEntryEntity> SaveAsync(Guid accountId, XpLedgerEntryEntity value, CancellationToken cancellationToken = default)
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
            || original.AccountId != current.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");
        if (current.Id != value.Id
            || current.AccountId != value.AccountId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        current.QuestOccurrenceEventId = value.QuestOccurrenceEventId;
        current.ContributionCode = value.ContributionCode;
        current.TrackCode = value.TrackCode;
        current.CategoryId = value.CategoryId;
        current.AttributeId = value.AttributeId;
        current.SystemSkillId = value.SystemSkillId;
        current.CustomSkillId = value.CustomSkillId;
        current.EffectiveAt = value.EffectiveAt;
        current.Amount = value.Amount;
        current.ProjectionVersion = value.ProjectionVersion;
        current.RulesetVersion = value.RulesetVersion;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var state = change.State;

        foreach (var entry in state.Ledger)
        {
            var completion = aggregate.Completions.SingleOrDefault(c => c.Id == entry.EventId);
            var eventId = entry.EventId;
            if (completion is null)
            {
                eventId = QuestValues.Derived(account.Id, $"penalty/{entry.OccurrenceId:D}/{entry.At:O}");
                await questOccurrenceEventService.AppendAsync(change, eventId, entry.OccurrenceId, "DeadlineElapsed", entry.At, token);
            }

            var baseAmount = completion is not null && entry.Track == "Category" ? completion.Snapshot!.BaseXp : entry.Amount;
            await StageContributionAsync(change, eventId, completion is null ? "Penalty" : "Base", entry.Track, entry.TrackId, entry.At, baseAmount, token);
            if (completion is not null && entry.Track == "Category" && entry.Amount != baseAmount)
                await StageContributionAsync(change, eventId, "StreakBonus", entry.Track, entry.TrackId, entry.At, entry.Amount - baseAmount, token);
        }

        data.RemoveRange((await data.GetByAccountIdAsync(account.Id, token)).Where(row => row.ProjectionVersion != change.Version));
    }

    private async Task StageContributionAsync(QuestCommit change, Guid eventId, string contribution, string track, string target, DateTimeOffset at, long amount, CancellationToken token)
    {
        var account = change.Account;
        var now = change.Now;

        var (system, custom) = track == "Skill" ? QuestValues.Skill(target) : default;
        await SaveAsync(
            account.Id,
            new XpLedgerEntryEntity
            {
                Id = QuestValues.Derived(account.Id, $"ledger/{eventId:D}/{contribution}/{track}/{target}"),
                AccountId = account.Id,
                QuestOccurrenceEventId = eventId,
                ContributionCode = contribution,
                TrackCode = track,
                CategoryId = track == "Category" ? target : null,
                AttributeId = track == "Attribute" ? target : null,
                SystemSkillId = system,
                CustomSkillId = custom,
                EffectiveAt = at,
                Amount = amount,
                ProjectionVersion = change.Version,
                RulesetVersion = "1.0",
                CreatedAt = now,
                ModifiedAt = now
            },
            token);
    }
}
