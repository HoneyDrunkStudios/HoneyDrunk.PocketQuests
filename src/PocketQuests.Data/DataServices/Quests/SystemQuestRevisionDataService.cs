using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>Frozen template queries and staged inserts.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SystemQuestRevisionDataService(AppDbContext context) : BaseDataService<SystemQuestRevisionEntity>(context), ISystemQuestRevisionDataService
{
    private IQueryable<SystemQuestRevisionEntity> Revisions => DbSet;

    /// <inheritdoc />
    public Task<SystemQuestRevisionEntity?> Read(Guid id, CancellationToken token) => Revisions.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, token);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemQuestRevisionEntity>> History(string questId, int before, int limit, CancellationToken token) =>
        await Revisions.AsNoTracking().Where(row => row.SystemQuestId == questId && row.Version < before)
            .OrderByDescending(row => row.Version).Take(limit).ToListAsync(token);
}
