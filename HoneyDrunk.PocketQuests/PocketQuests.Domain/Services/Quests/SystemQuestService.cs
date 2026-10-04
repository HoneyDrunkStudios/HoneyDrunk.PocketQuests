using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Domain.Services.Quests;

/// <summary>Retains SystemQuest ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class SystemQuestService(ISystemQuestDataService data) : ISystemQuestService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SystemQuestEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        data.GetCatalogAsync(cancellationToken);
}
