using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Quests.Mapping;

namespace PocketQuests.Services.Quests;

internal static class CommandHistoryChanges
{
    internal static QuestChanges Create(QuestMutation change, AccountIdentity identity, byte[] digest, AuditEntryId auditId, string? traceId)
    {
        var audit = AuditRecord.FromEntry(new AuditEntry(
            auditId,
            change.Now,
            identity.Subject,
            "pocketquests.quest." + change.Command.Action,
            AuditCategory.UserActivity,
            AuditOutcome.Succeeded,
            new AuditTarget("quest.account", change.Account.Id.ToString("N")),
            TenantId.Internal,
            traceId,
            AuditOperation.Update,
            Metadata: new Dictionary<string, string>
            {
                ["operationId"] = change.Command.OperationId.ToString("N"),
                ["rulesetVersion"] = "1.0",
            }));
        return new()
        {
            Receipt = change.IsInternal ? null : CommandHistoryMapping.ToReceipt(change, digest),
            History = CommandHistoryMapping.ToHistory(change),
            Audit = audit,
            AuditOwnership = CommandHistoryMapping.ToOwnership(change, audit.Id),
        };
    }
}
