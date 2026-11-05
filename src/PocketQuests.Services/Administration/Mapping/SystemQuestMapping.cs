using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Data.Queries.Administration;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Administration.Mapping;

internal static class SystemQuestMapping
{
    internal static AdminSystemQuest ToContract(this SystemQuestStudioRow row) => new(Guid.ParseExact(row.Head.Id[7..], "N"), Convert.ToBase64String(row.Head.RowVersion), row.Draft.Version, row.Draft.Id, row.Published?.Id, SystemQuestSnapshot.Read(row.Draft.SnapshotJson).ToModel(), row.Published is null ? null : SystemQuestSnapshot.Read(row.Published.SnapshotJson).ToModel(), row.Head.ModifiedAt);
}
