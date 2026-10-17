using PocketQuests.Domain.Models.Quests;

namespace PocketQuests.Services.Quests.Mapping;

internal sealed record CompactOutcome(DateTimeOffset ProjectionAt, CompletionOutcome? CompletionOutcome);
