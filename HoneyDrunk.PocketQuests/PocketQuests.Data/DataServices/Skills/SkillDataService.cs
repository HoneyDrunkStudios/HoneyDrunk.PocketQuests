using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for Skill.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SkillDataService(QuestDbContext context) : BaseDataService<SkillEntity>(context), ISkillDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderBy(row => row.SortOrder).ToListAsync(cancellationToken);
}
