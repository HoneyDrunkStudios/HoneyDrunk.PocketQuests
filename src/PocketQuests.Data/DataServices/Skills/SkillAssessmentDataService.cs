using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for SkillAssessment.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SkillAssessmentDataService(AppDbContext context) : BaseDataService<SkillAssessmentEntity>(context), ISkillAssessmentDataService
{
    private IQueryable<SkillAssessmentEntity> SkillAssessments => DbSet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillAssessmentEntity>> GetByAccountId(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await SkillAssessments.Where(row => row.AccountId == accountId).ToListAsync(cancellationToken);
    }
}
