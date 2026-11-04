using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using PocketQuests.Contracts.Models.Administration;
using PocketQuests.Contracts.Requests.Administration;
using PocketQuests.Contracts.Responses.Administration;
using PocketQuests.Data.DataServices.Accounts;
using PocketQuests.Data.DataServices.Quests;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Queries.Administration;
using PocketQuests.Domain.Errors;
using PocketQuests.Services.Administration.Mapping;
using PocketQuests.Services.Administration.Validators;
using PocketQuests.Services.Quests.Mapping;
using System.Diagnostics;
using ContractQuest = PocketQuests.Contracts.Models.Quests.Quest;

namespace PocketQuests.Services.Administration;

/// <summary>Audited immutable draft authoring with separate catalog pointers.</summary>
/// <param name="access">Configured operator authorization.</param>
/// <param name="accounts">Transaction and operator lifecycle ownership.</param>
/// <param name="templates">Catalog heads and bounded queries.</param>
/// <param name="revisions">Immutable template inserts.</param>
/// <param name="audits">Shared action audit and erasure ownership.</param>
/// <param name="clock">Trusted server clock.</param>
public sealed class AdminSystemQuestService(AdminOperator access, IAccountDataService accounts, ISystemQuestDataService templates,
    ISystemQuestRevisionDataService revisions, IAccountAuditRecordDataService audits, TimeProvider clock) : IAdminSystemQuestService
{
    /// <inheritdoc />
    public async Task<SystemQuestStudio> Read(CancellationToken token)
    {
        _ = access.Require();
        var rows = await templates.Studio(SystemQuestValidator.MaximumTemplates + 1, token);
        if (rows.Count > SystemQuestValidator.MaximumTemplates)
            throw new InvalidOperationException("The authored catalog exceeded its supported bound.");
        return new([.. rows.Select(row => row.ToContract())], PocketQuests.Services.Catalogs.Mapping.CatalogSnapshot.BuiltIn());
    }

    /// <inheritdoc />
    public ContractQuest Preview(SaveSystemQuestRequest request)
    {
        _ = access.Require();
        var quest = SystemQuestValidator.Normalize(request);
        SystemQuestValidator.RequireSnapshotSize(SystemQuestSnapshot.Freeze(quest));
        return quest.ToModel();
    }

    /// <inheritdoc />
    public async Task<AdminSystemQuest> Save(SaveSystemQuestRequest request, CancellationToken token)
    {
        var actor = access.Require();
        var quest = SystemQuestValidator.Normalize(request);
        var snapshot = SystemQuestSnapshot.Freeze(quest);
        SystemQuestValidator.RequireSnapshotSize(snapshot);
        var now = clock.GetUtcNow().ToUniversalTime();
        var result = await accounts.ExecuteInTransaction(
            async ct =>
        {
            await accounts.AcquireCommandLock(actor, ct);
            await accounts.AcquireCommandLock("system-quest-catalog", ct);
            var owner = await accounts.GetByIdentityUserId(actor, ct) ?? throw new QuestValidationException("Initialize your Pocket Quests profile before authoring studio quests.");
            if (await accounts.GetErasure(actor, ct) is not null || (await accounts.GetLifecycle(actor, ct)) is { StateCode: not "Active" })
                throw new UnauthorizedAccessException();
            var head = await templates.Editable(quest.Id, ct);
            SystemQuestRevisionEntity revision;
            if (head is null)
            {
                if (request.ExpectedVersion is not null)
                    throw new QuestNotFoundException();
                if ((await templates.Studio(SystemQuestValidator.MaximumTemplates, ct)).Count >= SystemQuestValidator.MaximumTemplates)
                    throw new QuestValidationException("The studio catalog supports at most 200 authored quests.");
                head = new() { Id = quest.Id, CategoryId = quest.CategoryId, Title = quest.Title, CatalogVersion = "admin-v1", CreatedAt = now, ModifiedAt = now, CreatedBy = actor, ModifiedBy = actor };
                revision = new() { Id = Guid.NewGuid(), SystemQuestId = quest.Id, Version = 1, SnapshotJson = snapshot, CreatedAt = now };
                await templates.AddInitialDraft(head, revision, ct);
            }
            else
            {
                var previous = await revisions.Read(head.DraftSystemQuestRevisionId ?? throw new InvalidOperationException("A studio draft pointer is required."), ct) ?? throw new InvalidOperationException("Draft content is missing.");

                // Exact retry of initial creation reuses its stable client identity.
                if (request.ExpectedVersion is null && previous.SnapshotJson == snapshot)
                    return new SystemQuestStudioRow(head, previous, head.PublishedSystemQuestRevisionId is { } live ? await revisions.Read(live, ct) : null);
                SystemQuestValidator.RequireVersion(request.ExpectedVersion, head.RowVersion);
                if (previous.Version >= SystemQuestValidator.MaximumRevisions)
                    throw new QuestValidationException("This quest has reached its 1000-revision limit. Create a new template identity for further content.");
                revision = new() { Id = Guid.NewGuid(), SystemQuestId = quest.Id, Version = previous.Version + 1, SnapshotJson = snapshot, CreatedAt = now };
                await revisions.AddAsync(revision, ct);
                head.DraftSystemQuestRevisionId = revision.Id;
                head.Title = quest.Title;
                head.CategoryId = quest.CategoryId;
                head.ModifiedAt = now > head.ModifiedAt ? now : head.ModifiedAt;
                head.ModifiedBy = actor;
            }

            await RecordAudit(owner.Id, actor, "pocketquests.admin.quest-draft", head.Id, now, ct);
            return new SystemQuestStudioRow(head, revision, head.PublishedSystemQuestRevisionId is { } published ? await revisions.Read(published, ct) : null);
        },
            token);
        return result.ToContract();
    }

    /// <inheritdoc />
    public async Task<AdminSystemQuest> Publish(PublishSystemQuestRequest request, CancellationToken token)
    {
        var actor = access.Require();
        if (request.Id == Guid.Empty || request.ExpectedDraftRevisionId == Guid.Empty)
            throw new QuestValidationException("Review a saved system quest draft before publishing.");
        var now = clock.GetUtcNow().ToUniversalTime();
        var result = await accounts.ExecuteInTransaction(
            async ct =>
        {
            await accounts.AcquireCommandLock(actor, ct);
            await accounts.AcquireCommandLock("system-quest-catalog", ct);
            var owner = await accounts.GetByIdentityUserId(actor, ct) ?? throw new UnauthorizedAccessException();
            if (await accounts.GetErasure(actor, ct) is not null || (await accounts.GetLifecycle(actor, ct)) is { StateCode: not "Active" })
                throw new UnauthorizedAccessException();
            var head = await templates.Editable(SystemQuestValidator.Code(request.Id), ct) ?? throw new QuestNotFoundException();
            SystemQuestValidator.RequireVersion(request.ExpectedVersion, head.RowVersion);
            if (head.DraftSystemQuestRevisionId != request.ExpectedDraftRevisionId)
                throw new QuestConflictException("The draft changed. Reload and review it before publishing.");
            var draft = await revisions.Read(request.ExpectedDraftRevisionId, ct) ?? throw new InvalidOperationException("Draft content is missing.");
            var target = request.Published ? draft.Id : (Guid?)null;
            if (head.PublishedSystemQuestRevisionId != target)
            {
                head.PublishedSystemQuestRevisionId = target;
                head.ModifiedAt = now > head.ModifiedAt ? now : head.ModifiedAt;
                head.ModifiedBy = actor;
                await RecordAudit(owner.Id, actor, request.Published ? "pocketquests.admin.quest-publish" : "pocketquests.admin.quest-withdraw", head.Id, now, ct);
            }

            return new SystemQuestStudioRow(head, draft, request.Published ? draft : null);
        },
            token);
        return result.ToContract();
    }

    private async Task RecordAudit(Guid accountId, string actor, string action, string questId, DateTimeOffset at, CancellationToken token)
    {
        var audit = AuditRecord.FromEntry(new AuditEntry(AuditEntryId.New(), at, actor, action, AuditCategory.UserActivity, AuditOutcome.Succeeded, new AuditTarget("quest.system-template", questId), TenantId.Internal, Activity.Current?.TraceId.ToString(), AuditOperation.Update));
        await audits.AddWithAudit(new AccountAuditRecordEntity { AccountId = accountId, AuditRecordId = audit.Id, CreatedAt = at }, audit, token);
    }
}
