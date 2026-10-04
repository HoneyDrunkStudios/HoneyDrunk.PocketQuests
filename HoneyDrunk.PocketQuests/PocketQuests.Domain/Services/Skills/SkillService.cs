using PocketQuests.Data.DataServices.Skills;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Domain.Services.Skills;

/// <summary>Retains Skill ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class SkillService(ISkillDataService data) : ISkillService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SkillEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        data.GetCatalogAsync(cancellationToken);
}
