using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Synchronization;
using PocketQuests.Data.Queries.Quests;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using System.Text.Json;
using QuestValues = PocketQuests.Domain.Services.Quests.QuestValues;

namespace PocketQuests.Services.Quests.Mapping;

internal static class CompletionHistoryMapping
{
    internal static QuestCompletionChanges ToChanges(this QuestCommit change, QuestCompletionRows rows, AccountIdentity identity, byte[] digest, AuditEntryId auditId, string? traceId)
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
            Receipt = change.IsInternal ? null : new CommandReceiptEntity
            {
                Id = change.Command.OperationId,
                AccountId = change.Account.Id,
                CommandType = change.Command.Action,
                ApiVersion = 1,
                DigestVersion = 1,
                PayloadDigest = digest,
                OutcomeVersion = 2,
                OutcomeJson = JsonSerializer.Serialize(new CompactOutcome(change.ProjectionAt, null)),
                AppliedMutationVersion = change.Version,
                CreatedAt = change.Now,
            },
            History = new QuestCommandHistoryEntity
            {
                Id = change.Command.OperationId,
                AccountId = change.Account.Id,
                CommandReceiptId = change.ReceiptId,
                AccountMutationVersion = change.Version,
                ReconciliationLimit = change.ReconciliationLimit,
                ActionReconciliationLimit = change.ActionReconciliationLimit,
                ActionCode = change.Command.Action,
                RulesetVersion = "1.0",
                TimeZoneBefore = change.TimeZoneBefore,
                ReconciledAt = change.ReconciledAt,
                RecordedAt = change.RecordedAt,
                ProjectionAt = change.ProjectionAt,
                QuestOccurrenceId = change.IsInternal ? null : change.Command.OccurrenceId,
                ConfirmPenalty = change.Command.ConfirmPenalty,
                AcceptedLoss = change.Command.AcceptedLoss,
                SourceSyncAnchorId = change.Command.RecordedTime?.AnchorId,
                CreatedAt = change.Now,
            },
            Audit = audit,
            AuditOwnership = new AccountAuditRecordEntity { AccountId = change.Account.Id, AuditRecordId = audit.Id, CreatedAt = change.Now },
        };
    }

    internal static void ApplyCompletions(this QuestCommit change, QuestCompletionRows rows, QuestCompletionChanges changes)
    {
        if (change.Command.RecordedTime is not null)
        {
            changes.History.CompletionTermsRevisionId = QuestValues.Derived(
                change.Account.Id,
                $"occurrence/{change.Command.OccurrenceId:D}/revision/{rows.Occurrences.Single(row => row.Id == change.Command.OccurrenceId).Revision}");
        }

        var existing = rows.Completions.Select(row => row.Id).ToHashSet();
        var occurrences = rows.Occurrences.Concat(changes.Occurrences).ToDictionary(row => row.Id);
        foreach (var completion in change.Aggregate.Completions.Where(item => !existing.Contains(item.Id)))
        {
            var occurrence = occurrences[completion.OccurrenceId];
            changes.Events.Add(CompletionOccurrenceMapping.ToEvent(change, completion.Id, completion.OccurrenceId, occurrence.Revision, "Completed", completion.RecordedAt));
            changes.Completions.Add(new QuestCompletionEntity
            {
                Id = completion.Id,
                AccountId = change.Account.Id,
                QuestOccurrenceId = completion.OccurrenceId,
                QuestOccurrenceRevisionId = QuestValues.Derived(change.Account.Id, $"occurrence/{occurrence.Id:D}/revision/{occurrence.Revision}"),
                RecordedAt = completion.RecordedAt,
                CreatedAt = change.Now,
                ModifiedAt = change.Now,
            });
        }
    }
}
