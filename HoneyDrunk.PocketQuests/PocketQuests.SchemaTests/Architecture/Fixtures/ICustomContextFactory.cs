namespace PocketQuests.SchemaTests.Architecture.Fixtures;

/// <summary>Compiled fixture for EF hidden behind a locally named interface.</summary>
internal interface ICustomContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<PocketQuests.Data.AppDbContext>;
