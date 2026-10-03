namespace PocketQuests.Data.Entities;

/// <summary>An account-owned definition with optimistic editing revision.</summary>
public sealed class DefinitionEntity
{
    /// <summary>Gets or sets the owning account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the stable definition ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the validated serialized definition.</summary>
    public string Document { get; set; } = string.Empty;
}
