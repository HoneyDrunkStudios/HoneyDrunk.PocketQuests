using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>Persistence operations and entity-specific queries for Skill.</summary>
public interface ISkillDataService : IBaseDataService<SkillEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<SkillEntity>> GetCatalog(CancellationToken cancellationToken = default);
}
