using PocketQuests.Data.Entities.Attributes;

namespace PocketQuests.Domain.Services.Attributes;

/// <summary>Business access and invariants for Attribute.</summary>
public interface IAttributeService
{
    /// <summary>Reads the entity data needed by this business service.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Matching persisted entities.</returns>
    Task<IReadOnlyList<AttributeEntity>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
