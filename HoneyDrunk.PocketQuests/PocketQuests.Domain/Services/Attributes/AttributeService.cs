using PocketQuests.Data.DataServices.Attributes;
using PocketQuests.Data.Entities.Attributes;

namespace PocketQuests.Domain.Services.Attributes;

/// <summary>Retains Attribute ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class AttributeService(IAttributeDataService data) : IAttributeService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AttributeEntity>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        data.GetCatalogAsync(cancellationToken);
}
