using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Quests;

internal static class QuestTransitions
{
    internal static (string code, DateTimeOffset at) Occurrence(Occurrence occurrence, QuestOccurrenceEntity? prior, DateTimeOffset recordedAt)
    {
        var life = occurrence.Lifecycle ?? new();
        var code = prior is null ? life.Unaccepted ? "Offered" : "Accepted"
            : life.AbandonedAt != prior.AbandonedAt ? "Abandoned"
            : life.FrozenAt is not null && life.FrozenAt != prior.FrozenAt ? "Frozen"
            : prior.FrozenAt is not null && life.FrozenAt is null ? "Resumed" : "Edited";
        var at = prior is null ? occurrence.AcceptedAt : code == "Abandoned" ? life.AbandonedAt!.Value : code == "Frozen" ? life.FrozenAt!.Value : recordedAt;
        return (code, at);
    }
}
