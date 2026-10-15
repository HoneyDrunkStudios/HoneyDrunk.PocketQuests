namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class BodyOnlyBusinessService
{
    internal static Type ContextType() => typeof(PocketQuests.Data.AppDbContext);
}
