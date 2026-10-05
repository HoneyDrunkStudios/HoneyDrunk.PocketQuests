using Microsoft.EntityFrameworkCore;
using PocketQuests.Data.Entities.Attributes;

namespace PocketQuests.Data.DataServices.Attributes;

/// <summary>EF persistence and queries for Attribute.</summary>
/// <param name="context">The scoped transaction context.</param>
public sealed class AttributeDataService(AppDbContext context) : BaseDataService<AttributeEntity>(context), IAttributeDataService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AttributeEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderBy(row => row.SortOrder).ToListAsync(cancellationToken);
}
