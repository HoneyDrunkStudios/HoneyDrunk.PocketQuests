using PocketQuests.Data.Entities.Synchronization;
using System.Text.Json;

namespace PocketQuests.Services.Quests.Mapping;

internal static class ReceiptMapping
{
    internal static CompactOutcome ToOutcome(this CommandReceiptEntity receipt) =>
        JsonSerializer.Deserialize<CompactOutcome>(receipt.OutcomeJson) ?? throw new InvalidOperationException("Receipt is invalid.");
}
