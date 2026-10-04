using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for CustomSkill.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class CustomSkillDataService(QuestDbContext context) : BaseDataService<CustomSkillEntity>(context), ICustomSkillDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomSkillEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomSkillEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default)
    {
        return await DbSet.AsNoTracking().Where(row => row.AccountId == accountId && ids.Contains(row.Id)).ToListAsync(token);
    }
}
