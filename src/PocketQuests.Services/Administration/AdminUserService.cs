using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Queries.Administration;
using PocketQuests.Domain.Errors;
using PocketQuests.Services.Administration.Mapping;
using PocketQuests.Services.Administration.Validators;
using System.Diagnostics;

namespace PocketQuests.Services.Administration;

/// <summary>Audits product access changes without changing Identity, progress or quest history.</summary>
/// <param name="access">Configured operator authorization.</param>
/// <param name="accounts">Account locks and transaction ownership.</param>
/// <param name="accountState">Scoped lifecycle and retained-state reads.</param>
/// <param name="restrictions">Current moderation records.</param>
/// <param name="audits">Shared audit and account-erasure association.</param>
/// <param name="clock">Trusted server clock.</param>
public sealed class AdminUserService(
    AdminOperator access,
    IAccountDataService accounts,
    IAccountStateDataService accountState,
    IAccountAccessRestrictionDataService restrictions,
    IAccountAuditRecordDataService audits,
    TimeProvider clock) : IAdminUserService
{
    /// <inheritdoc />
    public async Task<AdminUserPage> Search(string? query, string? after, CancellationToken token)
    {
        _ = access.Require();
        query = query?.Trim() ?? string.Empty;
        if (query.Length > 120 || (after is not null && !AdminUserValidator.CanonicalUser(after)))
            throw new QuestValidationException("Use a search of at most 120 characters and a valid page cursor.");
        var rows = await restrictions.Search(query, after, AdminUserValidator.PageSize + 1, token);
        return new([.. rows.Take(AdminUserValidator.PageSize).Select(row => row.ToContract())], rows.Count > AdminUserValidator.PageSize ? rows[AdminUserValidator.PageSize - 1].UserId : null);
    }

    /// <inheritdoc />
    public Task<AdminUser> SetAccess(SetUserAccessRequest request, CancellationToken token)
    {
        var actor = access.Require();
        AdminUserValidator.Validate(request);
        if (request.UserId == actor)
            throw new QuestValidationException("The configured operator cannot suspend or change their own administrative access.");
        var now = clock.GetUtcNow().ToUniversalTime();
        return accounts.ExecuteInTransaction(
            async ct =>
        {
            await accounts.AcquireCommandLock(request.UserId, ct);
            var account = await accounts.GetByIdentityUserId(request.UserId, ct) ?? throw new QuestNotFoundException();
            if (await accountState.GetErasure(request.UserId, ct) is not null)
                throw new QuestNotFoundException();
            var restriction = await restrictions.GetForAccount(account.Id, ct);
            if ((restriction?.Revision ?? 0) != request.ExpectedRevision)
                throw new QuestConflictException("The account status changed. Reload before making another change.");
            if (restriction is null)
            {
                restriction = new() { Id = Guid.NewGuid(), AccountId = account.Id, CreatedAt = now, CreatedBy = actor };
                await restrictions.AddAsync(restriction, ct);
            }

            restriction.IsSuspended = request.Suspended;
            restriction.Reason = request.Reason.Trim();
            restriction.Revision++;
            restriction.ModifiedAt = now > restriction.ModifiedAt ? now : restriction.ModifiedAt;
            restriction.ModifiedBy = actor;
            var audit = AuditRecord.FromEntry(new AuditEntry(AuditEntryId.New(), now, actor, request.Suspended ? "pocketquests.admin.suspend" : "pocketquests.admin.reactivate", AuditCategory.UserActivity, AuditOutcome.Succeeded, new AuditTarget("quest.account", account.Id.ToString("N")), TenantId.Internal, Activity.Current?.TraceId.ToString(), AuditOperation.Update));
            await audits.AddWithAudit(new AccountAuditRecordEntity { AccountId = account.Id, AuditRecordId = audit.Id, CreatedAt = now }, audit, ct);
            return new AdminUserRow(account.IdentityUserId, account.CharacterName, account.TimeZoneId, account.IsOnboardingComplete, account.CreatedAt, restriction.IsSuspended, restriction.Reason, restriction.Revision).ToContract();
        },
            token);
    }
}
