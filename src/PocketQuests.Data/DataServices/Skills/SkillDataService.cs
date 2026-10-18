using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for Skill.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SkillDataService(AppDbContext context) : BaseDataService<SkillEntity>(context), ISkillDataService
{
    private IQueryable<SkillEntity> Skills => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillEntity>> GetCatalog(CancellationToken cancellationToken = default) =>
        await Skills.OrderBy(row => row.SortOrder).ToListAsync(cancellationToken);
}
