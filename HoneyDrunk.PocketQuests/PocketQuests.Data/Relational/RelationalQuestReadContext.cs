using Microsoft.EntityFrameworkCore;

namespace PocketQuests.Data.Relational;

/// <summary>
/// Read-only mapping for the relational DACPAC, selected by the relational persistence service.
/// Mutations use named controlled procedures; this EF context never writes account entities.
/// </summary>
/// <param name="options">The explicitly supplied database connection.</param>
public sealed class RelationalQuestReadContext(DbContextOptions<RelationalQuestReadContext> options) : DbContext(options)
{
    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("The relational context is read-only; use the reviewed controlled writer.");

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The relational context is read-only; use the reviewed controlled writer.");

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => RelationalQuestModel.Configure(modelBuilder);
}
