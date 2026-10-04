using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Domain.Services.Skills;

/// <summary>Business access and invariants for Skill.</summary>
public interface ISkillService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<SkillEntity>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
