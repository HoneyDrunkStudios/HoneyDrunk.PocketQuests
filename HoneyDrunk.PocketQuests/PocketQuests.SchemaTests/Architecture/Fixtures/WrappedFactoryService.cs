namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class WrappedFactoryService(ICustomContextFactory factory)
{
    internal object Factory => factory;
}
