using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.DataServices.Skills;

/// <summary>EF persistence and queries for SkillAssessment.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class SkillAssessmentDataService(AppDbContext context) : BaseDataService<SkillAssessmentEntity>(context), ISkillAssessmentDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillAssessmentEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await LoadAccountCollectionAsync(accountId, DbSet.Where(row => row.AccountId == accountId), cancellationToken);
        return DbSet.Local.Where(row => row.AccountId == accountId).ToArray();
    }
}
