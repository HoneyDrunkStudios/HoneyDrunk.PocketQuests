using HoneyDrunk.Audit.Data;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.DataServices.Quests;

/// <summary>New/deleted entities for one account operation. Existing acquired entities use ordinary EF tracking.</summary>
public sealed class QuestChanges
{
    /// <summary>Gets new occurrence heads.</summary>
    public List<QuestOccurrenceEntity> Occurrences { get; } = [];

    /// <summary>Gets new immutable occurrence revisions.</summary>
    public List<QuestOccurrenceRevisionEntity> Revisions { get; } = [];

    /// <summary>Gets new immutable source events.</summary>
    public List<QuestOccurrenceEventEntity> Events { get; } = [];

    /// <summary>Gets new completions.</summary>
    public List<QuestCompletionEntity> Completions { get; } = [];

    /// <summary>Gets new ledger projections.</summary>
    public List<XpLedgerEntryEntity> Ledger { get; } = [];

    /// <summary>Gets obsolete ledger projections.</summary>
    public List<XpLedgerEntryEntity> RemovedLedger { get; } = [];

    /// <summary>Gets new balance projections.</summary>
    public List<XpBalanceEntity> Balances { get; } = [];

    /// <summary>Gets obsolete balance projections.</summary>
    public List<XpBalanceEntity> RemovedBalances { get; } = [];

    /// <summary>Gets new category projections.</summary>
    public List<CategoryProgressEntity> Categories { get; } = [];

    /// <summary>Gets obsolete category projections.</summary>
    public List<CategoryProgressEntity> RemovedCategories { get; } = [];

    /// <summary>Gets new reward projections.</summary>
    public List<AccountEntitlementEntity> Entitlements { get; } = [];

    /// <summary>Gets obsolete reward projections.</summary>
    public List<AccountEntitlementEntity> RemovedEntitlements { get; } = [];

    /// <summary>Gets the compact receipt, absent for bounded internal reconciliation.</summary>
    public CommandReceiptEntity? Receipt { get; init; }

    /// <summary>Gets the immutable source command.</summary>
    public required QuestCommandHistoryEntity History { get; init; }

    /// <summary>Gets the shared audit record.</summary>
    public required AuditRecord Audit { get; init; }

    /// <summary>Gets explicit ownership of that audit record.</summary>
    public required AccountAuditRecordEntity AuditOwnership { get; init; }

    /// <summary>Gets the staged Definitions entities.</summary>
    public List<QuestDefinitionEntity> Definitions { get; } = [];

    /// <summary>Gets the staged DefinitionRevisions entities.</summary>
    public List<QuestDefinitionRevisionEntity> DefinitionRevisions { get; } = [];

    /// <summary>Gets the staged DefinitionAttributes entities.</summary>
    public List<QuestDefinitionAttributeAllocationEntity> DefinitionAttributes { get; } = [];

    /// <summary>Gets the staged DefinitionSkills entities.</summary>
    public List<QuestDefinitionSkillAllocationEntity> DefinitionSkills { get; } = [];

    /// <summary>Gets the staged Series entities.</summary>
    public List<QuestSeriesEntity> Series { get; } = [];

    /// <summary>Gets the staged SeriesRevisions entities.</summary>
    public List<QuestSeriesRevisionEntity> SeriesRevisions { get; } = [];

    /// <summary>Gets the staged Skills entities.</summary>
    public List<CustomSkillEntity> Skills { get; } = [];

    /// <summary>Gets the staged Interests entities.</summary>
    public List<AccountInterestEntity> Interests { get; } = [];

    /// <summary>Gets the staged RemovedInterests entities.</summary>
    public List<AccountInterestEntity> RemovedInterests { get; } = [];

    /// <summary>Gets the staged Pauses entities.</summary>
    public List<AccountPauseEntity> Pauses { get; } = [];

    /// <summary>Gets the staged Assessments entities.</summary>
    public List<SkillAssessmentEntity> Assessments { get; } = [];

    /// <summary>Gets the staged Zones entities.</summary>
    public List<TimeZoneChangeEntity> Zones { get; } = [];

    /// <summary>Gets the staged CommandInterests entities.</summary>
    public List<QuestCommandInterestEntity> CommandInterests { get; } = [];
}
