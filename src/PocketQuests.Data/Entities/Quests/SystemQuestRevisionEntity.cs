namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one immutable operator-authored public system-quest template revision. Classification: Public. History: insert-only; the row is the content history, while shared Audit records the actor separately. Retain referenced public content; at most 1000 revisions per authored identity and 200 authored identities.</summary>
public sealed class SystemQuestRevisionEntity
{
    /// <summary>Gets or sets the Application-generated stable identity of this immutable template revision.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the Stable system quest identity owning this revision; never reused.</summary>
    public string SystemQuestId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Monotonic content revision from 1 through 1000 within this system quest.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the Frozen complete quest terms under ruleset 1.0. Display and replay only, never queried as relational fields; valid JSON at most 32768 UTF-16 bytes.</summary>
    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>Gets or sets the Trusted UTC insertion instant of this immutable content revision.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
