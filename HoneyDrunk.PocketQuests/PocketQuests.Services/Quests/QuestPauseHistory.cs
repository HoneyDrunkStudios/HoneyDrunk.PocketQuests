using PocketQuests.Data.Queries.Quests;

namespace PocketQuests.Services.Quests;

internal static class QuestPauseHistory
{
    internal static IReadOnlyList<string> Resolve(QuestStateRows rows)
    {
        var order = new List<string>();
        foreach (var input in rows.PauseHistory.Where(row => row.CategoryId is not null && row.ActionCode is "pause" or "resume"))
        {
            if (input.ActionCode == "resume")
                order.Remove(input.CategoryId!);
            else if (!order.Contains(input.CategoryId!))
                order.Add(input.CategoryId!);
        }

        if (!order.Order().SequenceEqual(rows.CategoryProgress.Where(row => row.IsExplicitlyPaused).Select(row => row.CategoryId).Order()))
            throw new InvalidOperationException("Explicit pause projection differs from retained preference history.");
        return order;
    }
}
