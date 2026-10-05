using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.Queries.Quests;

/// <summary>Tracked materialized projections acquired only when a mutation will be staged.</summary>
public sealed class QuestProjectionRows
{
    /// <summary>Gets the owned XpLedgerEntry rows.</summary>
    public required IReadOnlyList<XpLedgerEntryEntity> Ledger { get; init; }

    /// <summary>Gets the owned XpBalance rows.</summary>
    public required IReadOnlyList<XpBalanceEntity> Balances { get; init; }

    /// <summary>Gets the owned AccountEntitlement rows.</summary>
    public required IReadOnlyList<AccountEntitlementEntity> Entitlements { get; init; }
}
