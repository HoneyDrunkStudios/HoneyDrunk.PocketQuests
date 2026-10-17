namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class AsyncEndpoint
{
    internal static async Task<Type> Read()
    {
        await Task.Yield();
        return typeof(PocketQuests.Domain.Quests.Aggregates.QuestAggregate);
    }
}
