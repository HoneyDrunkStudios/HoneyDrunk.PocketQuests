namespace PocketQuests.Data.Entities;

/// <summary>An immutable definition revision, retained for private account history/export.</summary>
public sealed class DefinitionRevisionEntity
{
    /// <summary>Gets or sets the owning account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the stable definition ID.</summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the monotonically increasing edit revision.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets the immutable validated revision document.</summary>
    public string Document { get; set; } = string.Empty;
}
