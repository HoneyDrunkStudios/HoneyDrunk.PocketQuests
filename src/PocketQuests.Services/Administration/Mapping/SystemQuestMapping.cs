using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Data.Queries.Administration;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Quests.Mapping;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Services.Administration.Mapping;

internal static class SystemQuestMapping
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    internal static string Freeze(Quest quest) => JsonSerializer.Serialize(quest, Json);

    internal static Quest Read(string snapshot) => JsonSerializer.Deserialize<Quest>(snapshot, Json) ?? throw new InvalidOperationException("Missing immutable system quest terms.");

    internal static AdminSystemQuest ToContract(this SystemQuestStudioRow row) => new(Guid.ParseExact(row.Head.Id[7..], "N"), Convert.ToBase64String(row.Head.RowVersion), row.Draft.Version, row.Draft.Id, row.Published?.Id, Read(row.Draft.SnapshotJson).ToModel(), row.Published is null ? null : Read(row.Published.SnapshotJson).ToModel(), row.Head.ModifiedAt);
}
