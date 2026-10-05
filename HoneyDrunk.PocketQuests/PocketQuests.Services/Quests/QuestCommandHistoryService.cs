using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.DataServices.Synchronization;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Services.Quests.Mapping;
using QuestValues = PocketQuests.Domain.Quests.QuestValues;

namespace PocketQuests.Services.Quests;

internal sealed class QuestCommandHistoryService(IQuestCommandHistoryDataService historyData,
    ICommandReceiptDataService receiptData, IAccountAuditRecordDataService auditData)
{
    internal async Task Record(QuestMutation change, AccountIdentity identity, byte[] digest, AuditEntryId auditId, string? traceId, QuestTermHistory terms, IReadOnlyList<QuestOccurrenceEntity> occurrences, CancellationToken token)
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
        var (target, quest) = QuestHistory.CommandTargets(change);
        var command = change.Command;
        var termId = quest is null ? (Guid?)null : terms.Find(quest).Id;
        var occurrence = command.Action == QuestActions.Complete && command.RecordedTime is not null
            ? occurrences.Single(row => row.Id == target!.Id) : null;
        var completionRevision = occurrence is null ? (Guid?)null : QuestValues.Derived(change.Account.Id, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}");
        var skillName = command.Action == QuestActions.SaveSkill ? change.Aggregate.Profile.CustomSkills!.Single(row => row.Id == command.SkillId).Name : null;
        var history = CommandHistoryMapping.ToHistory(change);
        change.ApplyToHistory(history, target, quest, termId, completionRevision, skillName);
        history.CreatedAt = change.Now;
        await historyData.AddAsync(history, token);
        if (!change.IsInternal)
        {
            var receipt = CommandHistoryMapping.ToReceipt(change, digest);
            receipt.CreatedAt = change.Now;
            await receiptData.AddAsync(receipt, token);
        }

        var ownership = CommandHistoryMapping.ToOwnership(change, audit.Id);
        ownership.CreatedAt = change.Now;
        await auditData.AddWithAudit(ownership, audit, token);
    }
}
