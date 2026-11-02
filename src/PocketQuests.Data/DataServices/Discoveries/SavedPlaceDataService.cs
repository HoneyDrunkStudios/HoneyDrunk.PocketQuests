using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Discoveries;

namespace PocketQuests.Data.DataServices.Discoveries;

/// <summary>SQL-filtered saved-place queries sharing the account transaction context.</summary>
public sealed class SavedPlaceDataService(AppDbContext context) : BaseDataService<SavedPlaceEntity>(context), ISavedPlaceDataService
{
    private IQueryable<SavedPlaceEntity> SavedPlaces => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SavedPlaceEntity>> GetByAccountId(Guid accountId, CancellationToken token = default) =>
        await SavedPlaces.Where(row => row.AccountId == accountId).OrderByDescending(row => row.ModifiedAt).ThenBy(row => row.Id).Take(100).ToListAsync(token);

    /// <inheritdoc />
    public Task<bool> CanLink(Guid accountId, Guid occurrenceId, CancellationToken token = default) =>
        Context.QuestOccurrence.AnyAsync(row => row.AccountId == accountId && row.Id == occurrenceId && row.AcceptedAt != null, token);
}
