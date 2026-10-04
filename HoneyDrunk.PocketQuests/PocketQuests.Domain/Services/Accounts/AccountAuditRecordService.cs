using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using System.Diagnostics;

namespace PocketQuests.Domain.Services.Accounts;

/// <summary>Retains AccountAuditRecord ownership and history while staging ordinary EF changes.</summary>
/// <param name="data">Scoped entity persistence.</param>
public sealed class AccountAuditRecordService(IAccountAuditRecordDataService data) : IAccountAuditRecordService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<AccountAuditRecordEntity>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        data.GetByAccountIdAsync(accountId, cancellationToken);

    /// <inheritdoc />
    public async Task<AccountAuditRecordEntity> SaveAsync(Guid accountId, AccountAuditRecordEntity value, CancellationToken cancellationToken = default)
    {
        if (value.AccountId != accountId)
            throw new UnauthorizedAccessException("Entity ownership differs from the resolved account.");
        var current = await data.FindByIdAsync(new object[] { value.AccountId, value.AuditRecordId }, cancellationToken);
        if (current is null)
        {
            await data.AddAsync(value, cancellationToken);
            return value;
        }

        var original = data.GetOriginalValues(current);
        if (original.CreatedAt != current.CreatedAt)
            throw new InvalidOperationException("Original insertion time cannot be changed.");

        if (original.AccountId != current.AccountId
            || original.AuditRecordId != current.AuditRecordId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        if (current.AccountId != value.AccountId
            || current.AuditRecordId != value.AuditRecordId)
            throw new InvalidOperationException("Committed identity or historical values cannot be changed.");

        return current;
    }

    /// <inheritdoc />
    public async Task RecordCommandAsync(AccountIdentity identity, QuestCommit change, CancellationToken token = default)
    {
        var audit = AuditRecord.FromEntry(new AuditEntry(
            AuditEntryId.New(),
            change.Now,
            identity.Subject,
            "pocketquests.quest." + change.Command.Action,
            AuditCategory.UserActivity,
            AuditOutcome.Succeeded,
            new AuditTarget("quest.account", change.Account.Id.ToString("N")),
            TenantId.Internal,
            Activity.Current?.TraceId.ToString(),
            AuditOperation.Update,
            Metadata: new Dictionary<string, string>
            {
                ["operationId"] = change.Command.OperationId.ToString("N"),
                ["rulesetVersion"] = "1.0",
            }));
        await data.AddWithAuditAsync(new AccountAuditRecordEntity { AccountId = change.Account.Id, AuditRecordId = audit.Id, CreatedAt = change.Now }, audit, token);
    }
}
