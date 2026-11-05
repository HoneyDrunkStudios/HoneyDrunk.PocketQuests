namespace PocketQuests.Data.Entities.Discoveries;

/// <summary>One row is one private place saved by an account. Classification: Restricted. History: mutable current record with creation/modification stamps and rowversion; action audit contains no notes or coordinates. At most 100 per account enforced under the account lock; delete on removal or final account erasure. No old note copies or location tracking.</summary>
public sealed class SavedPlaceEntity
{
    /// <summary>Gets or sets Application-generated stable identifier for this saved place.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets Product account that owns the private discovery; supplied only by trusted server context.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets OpenStreetMap element key, node/number, way/number or relation/number; external identifier, not a product foreign key.</summary>
    public string PlaceKey { get; set; } = string.Empty;

    /// <summary>Gets or sets Player-visible place name captured when saved; not an assurance of current provider accuracy.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets Supported place category label: Parks, Gardens, Libraries, Museums, Art, Community, Markets or Viewpoints.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets Quest category associated with this type of place; grants no progress.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets Saved place latitude in degrees, between minus 85 and 85; not a player movement history.</summary>
    public decimal Latitude { get; set; }

    /// <summary>Gets or sets Saved place longitude in degrees, between minus 180 and 180.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Gets or sets Private player-authored discovery notes; empty string means no notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets Server UTC instant when the player marked this place visited; null means want to visit. Self-report, not GPS proof.</summary>
    public DateTimeOffset? VisitedAt { get; set; }

    /// <summary>Gets or sets Optional accepted quest owned by the same account; null means no linked quest. Completion remains explicit.</summary>
    public Guid? QuestOccurrenceId { get; set; }

    /// <summary>Gets or sets Positive optimistic edit revision; stale writes must reload rather than replace newer edits.</summary>
    public int Revision { get; set; }

    /// <summary>Gets or sets Server UTC instant when this saved-place record was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets Server UTC instant of the most recent saved-place edit; never before CreatedAt.</summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>Gets or sets SQL Server optimistic concurrency token; not a timestamp.</summary>
    public byte[] RowVersion { get; set; } = [];
}
