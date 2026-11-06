using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Quests;

internal static class QuestTransitions
{
    internal static (string code, DateTimeOffset at) Occurrence(Occurrence occurrence, QuestOccurrenceEntity? prior, DateTimeOffset recordedAt)
    {
        var life = occurrence.Lifecycle ?? new();
        if (prior is null)
            return (code: life.Unaccepted ? "Offered" : "Accepted", at: occurrence.AcceptedAt);
        if (life.AbandonedAt != prior.AbandonedAt)
            return (code: "Abandoned", at: life.AbandonedAt!.Value);
        if (life.FrozenAt is not null && life.FrozenAt != prior.FrozenAt)
            return (code: "Frozen", at: life.FrozenAt.Value);
        if (prior.FrozenAt is not null && life.FrozenAt is null)
            return (code: "Resumed", at: recordedAt);
        return (code: "Edited", at: recordedAt);
    }
}
