using PocketQuests.Contracts.Responses.Catalogs;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Services.Catalogs.Mapping;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Catalogs;

/// <summary>Combines source-controlled starter terms with only published studio revisions.</summary>
/// <param name="templates">Published immutable template queries.</param>
public sealed class CatalogService(ISystemQuestDataService templates) : ICatalogService
{
    /// <inheritdoc />
    public async Task<CatalogResponse> Read(CancellationToken token = default)
    {
        var published = await templates.Published(QuestRules.MaximumSystemTemplates + 1, token);
        if (published.Count > QuestRules.MaximumSystemTemplates)
            throw new InvalidOperationException("The published studio catalog exceeded its supported bound.");
        var starters = CatalogSnapshot.BuiltIn();
        return starters with { Quests = [.. starters.Quests, .. published.Select(row => SystemQuestSnapshot.Read(row.SnapshotJson).ToModel())] };
    }
}
