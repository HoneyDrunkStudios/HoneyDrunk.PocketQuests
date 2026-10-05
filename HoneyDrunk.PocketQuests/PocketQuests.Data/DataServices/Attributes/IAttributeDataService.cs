using PocketQuests.Data.Entities.Attributes;

namespace PocketQuests.Data.DataServices.Attributes;

/// <summary>Persistence operations and entity-specific queries for Attribute.</summary>
public interface IAttributeDataService : IBaseDataService<AttributeEntity>
{
    /// <summary>Gets the versioned public catalog.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching tracked entities.</returns>
    Task<IReadOnlyList<AttributeEntity>> GetCatalog(CancellationToken cancellationToken = default);
}
