using PocketQuests.Data.DataServices.Categories;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Services.Quests;

namespace PocketQuests.Domain.Services.Categories;

/// <summary>Retains CategoryProgress ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class CategoryProgressService(ICategoryProgressDataService data) : ICategoryProgressService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<CategoryProgressEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<CategoryProgressEntity> SaveAsync(Guid accountId, CategoryProgressEntity value, CancellationToken cancellationToken = default)
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

        current.CategoryId = value.CategoryId;
        current.StreakDays = value.StreakDays;
        current.BonusRatePercent = value.BonusRatePercent;
        current.HasQualifiedToday = value.HasQualifiedToday;
        current.IsExplicitlyPaused = value.IsExplicitlyPaused;
        current.AsOfDate = value.AsOfDate;
        current.TimeZoneId = value.TimeZoneId;
        current.ProjectionVersion = value.ProjectionVersion;
        current.ModifiedAt = current.ModifiedAt > value.ModifiedAt ? current.ModifiedAt : value.ModifiedAt;
        return current;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(QuestCommit change, CancellationToken token = default)
    {
        var account = change.Account;
        var aggregate = change.Aggregate;
        var state = change.State;
        var now = change.Now;

        foreach (var streak in state.Streaks)
        {
            await SaveAsync(
                account.Id,
                new CategoryProgressEntity
                {
                    Id = QuestValues.Derived(account.Id, "streak/" + streak.CategoryId),
                    AccountId = account.Id,
                    CategoryId = streak.CategoryId,
                    StreakDays = streak.Days,
                    BonusRatePercent = streak.Rate,
                    HasQualifiedToday = streak.QualifiedToday,
                    IsExplicitlyPaused = aggregate.Schedule.PausedCategories.Contains(streak.CategoryId),
                    AsOfDate = QuestValues.Date(state.Today)!.Value,
                    TimeZoneId = aggregate.Zone,
                    ProjectionVersion = change.Version,
                    CreatedAt = now,
                    ModifiedAt = now
                },
                token);
        }

        data.RemoveRange((await data.GetByAccountIdAsync(account.Id, token)).Where(row => row.ProjectionVersion != change.Version));
    }
}
