using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>Persistence operations and entity-specific queries for CustomSkill.</summary>
public interface ICustomSkillDataService : IBaseDataService<CustomSkillEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<CustomSkillEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads only referenced account-owned skills.</summary>
    /// <param name="accountId">Resolved account.</param>
    /// <param name="ids">Referenced identifiers.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Matching committed skills.</returns>
    Task<IReadOnlyList<CustomSkillEntity>> GetSelectedAsync(Guid accountId, Guid[] ids, CancellationToken token = default);
}
