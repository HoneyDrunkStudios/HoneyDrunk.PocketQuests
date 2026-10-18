using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for CustomSkill.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CustomSkillDataService(AppDbContext context) : BaseDataService<CustomSkillEntity>(context), ICustomSkillDataService
{
    private IQueryable<CustomSkillEntity> CustomSkills => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomSkillEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await CustomSkills.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomSkillEntity>> GetSelected(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await CustomSkills.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
