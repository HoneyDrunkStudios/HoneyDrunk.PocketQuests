namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal abstract class ConstrainedBusinessService<TContext>
    where TContext : Microsoft.EntityFrameworkCore.DbContext;
