namespace PocketQuests.Data.Entities.Quests;

/// <summary>One row is one stable system quest identity whose full template is owned by the versioned catalog. Classification: Public. History: reference; Retain referenced codes; source-controlled template versions retain old terms. Not erased with accounts.</summary>
public sealed class SystemQuestEntity
{
    /// <summary>Gets or sets stable system quest code, for example PQ-CAT-Q01; never reused.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets canonical category of the system template.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets current public system-quest title; accepted terms use immutable account definition revisions.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets reviewed template version; seeds must agree with the canonical catalog.</summary>
    public string CatalogVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets server UTC insertion time; not the effective time of a delayed offline action.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets server UTC time of the most recent persisted change; writer must set on each update.</summary>
    public DateTimeOffset ModifiedAt { get; set; }
}
