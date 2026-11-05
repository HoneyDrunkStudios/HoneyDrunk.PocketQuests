using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Services.Quests.Mapping;

internal static class SystemQuestSnapshot
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    internal static string Freeze(Quest quest) => JsonSerializer.Serialize(quest, Json);

    internal static Quest Read(string snapshot) => JsonSerializer.Deserialize<Quest>(snapshot, Json) ?? throw new InvalidOperationException("Missing immutable system quest terms.");
}
