using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>Persistence operations and entity-specific queries for SkillAssessment.</summary>
public interface ISkillAssessmentDataService : IBaseDataService<SkillAssessmentEntity>
{
    /// <summary>Gets rows owned by the resolved product account.</summary>
    /// <param name="accountId">Resolved product account identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<SkillAssessmentEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default);
}
