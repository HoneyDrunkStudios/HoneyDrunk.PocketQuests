namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class GenericContextService(Microsoft.EntityFrameworkCore.IDbContextFactory<PocketQuests.Data.AppDbContext> factory)
{
    internal object Factory => factory;
}
