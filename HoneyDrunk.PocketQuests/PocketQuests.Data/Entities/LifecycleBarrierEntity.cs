namespace PocketQuests.Data.Entities;

/// <summary>Fences delayed lifecycle messages and product commands under the account lock.</summary>
public sealed class LifecycleBarrierEntity
{
    /// <summary>Gets or sets the canonical Identity account ID.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the latest applied Identity version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets Active or Inactive; erased accounts retain only a separate marker.</summary>
    public string State { get; set; } = string.Empty;
}
