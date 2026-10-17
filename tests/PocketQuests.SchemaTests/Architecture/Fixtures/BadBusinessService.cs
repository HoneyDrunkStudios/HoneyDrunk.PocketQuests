namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class BadBusinessService(PocketQuests.Data.AppDbContext context)
{
    internal object Context => context;
}
