namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class ValidEndpoint
{
    internal static Task<PocketQuests.Contracts.Responses.Projections.QuestState> Read(PocketQuests.Services.Quests.IQuestService service, CancellationToken token) => service.Read(token);
}
