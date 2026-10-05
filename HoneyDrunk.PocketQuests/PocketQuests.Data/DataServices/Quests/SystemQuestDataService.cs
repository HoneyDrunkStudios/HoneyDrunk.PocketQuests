using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>EF persistence and queries for SystemQuest.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SystemQuestDataService(AppDbContext context) : BaseDataService<SystemQuestEntity>(context), ISystemQuestDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemQuestEntity>> GetCatalog(CancellationToken cancellationToken = default) =>
        await DbSet.ToListAsync(cancellationToken);
}
