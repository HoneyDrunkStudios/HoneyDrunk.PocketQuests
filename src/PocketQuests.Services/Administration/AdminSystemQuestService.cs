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
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Services.Administration.Mapping;
using PocketQuests.Services.Administration.Validators;
using PocketQuests.Services.Catalogs;
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
        return new([.. rows.Select(row => row.ToContract())], new CatalogService().Read());
    }

    /// <inheritdoc />
    public ContractQuest Preview(SaveSystemQuestRequest request)
    {
        _ = access.Require();
        var quest = SystemQuestValidator.Normalize(request);
        SystemQuestValidator.RequireSnapshotSize(SystemQuestMapping.Freeze(quest));
        return quest.ToModel();
    }

    /// <inheritdoc />
    public async Task<AdminSystemQuest> Save(SaveSystemQuestRequest request, CancellationToken token)
    {
        var actor = access.Require();
        var quest = SystemQuestValidator.Normalize(request);
        var snapshot = SystemQuestMapping.Freeze(quest);
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
                (head, revision) = await CreateInitialDraft(quest, request.ExpectedVersion, snapshot, actor, now, ct);
            }
            else
            {
                var previous = await ReadDraft(head, ct);

                // Exact retry of initial creation reuses its stable client identity.
                if (request.ExpectedVersion is null && previous.SnapshotJson == snapshot)
                    return await StudioRow(head, previous, ct);
                SystemQuestValidator.RequireVersion(request.ExpectedVersion, head.RowVersion);
                revision = await AppendDraft(head, previous, quest, snapshot, actor, now, ct);
            }

            await RecordAudit(owner.Id, actor, "pocketquests.admin.quest-draft", head.Id, now, ct);
            return await StudioRow(head, revision, ct);
        },
            token);
        return result.ToContract();
    }

    private async Task<(SystemQuestEntity head, SystemQuestRevisionEntity revision)> CreateInitialDraft(Quest quest, string? expectedVersion, string snapshot, string actor, DateTimeOffset now, CancellationToken token)
    {
        if (expectedVersion is not null)
            throw new QuestNotFoundException();
        if ((await templates.Studio(SystemQuestValidator.MaximumTemplates, token)).Count >= SystemQuestValidator.MaximumTemplates)
            throw new QuestValidationException("The studio catalog supports at most 200 authored quests.");
        var head = new SystemQuestEntity { Id = quest.Id, CategoryId = quest.CategoryId, Title = quest.Title, CatalogVersion = "admin-v1", CreatedAt = now, ModifiedAt = now, CreatedBy = actor, ModifiedBy = actor };
        var revision = new SystemQuestRevisionEntity { Id = Guid.NewGuid(), SystemQuestId = quest.Id, Version = 1, SnapshotJson = snapshot, CreatedAt = now };
        await templates.AddInitialDraft(head, revision, token);
        return (head, revision);
    }

    private async Task<SystemQuestRevisionEntity> ReadDraft(SystemQuestEntity head, CancellationToken token) =>
        await revisions.Read(head.DraftSystemQuestRevisionId ?? throw new InvalidOperationException("A studio draft pointer is required."), token) ?? throw new InvalidOperationException("Draft content is missing.");

    private async Task<SystemQuestRevisionEntity> AppendDraft(SystemQuestEntity head, SystemQuestRevisionEntity previous, Quest quest, string snapshot, string actor, DateTimeOffset now, CancellationToken token)
    {
        if (previous.Version >= SystemQuestValidator.MaximumRevisions)
            throw new QuestValidationException("This quest has reached its 1000-revision limit. Create a new template identity for further content.");
        var revision = new SystemQuestRevisionEntity { Id = Guid.NewGuid(), SystemQuestId = quest.Id, Version = previous.Version + 1, SnapshotJson = snapshot, CreatedAt = now };
        await revisions.AddAsync(revision, token);
        head.DraftSystemQuestRevisionId = revision.Id;
        head.Title = quest.Title;
        head.CategoryId = quest.CategoryId;
        head.ModifiedAt = now > head.ModifiedAt ? now : head.ModifiedAt;
        head.ModifiedBy = actor;
        return revision;
    }

    private async Task<SystemQuestStudioRow> StudioRow(SystemQuestEntity head, SystemQuestRevisionEntity draft, CancellationToken token) =>
        new(head, draft, head.PublishedSystemQuestRevisionId is { } published ? await revisions.Read(published, token) : null);

    private async Task RecordAudit(Guid accountId, string actor, string action, string questId, DateTimeOffset at, CancellationToken token)
    {
        var audit = AuditRecord.FromEntry(new AuditEntry(AuditEntryId.New(), at, actor, action, AuditCategory.UserActivity, AuditOutcome.Succeeded, new AuditTarget("quest.system-template", questId), TenantId.Internal, Activity.Current?.TraceId.ToString(), AuditOperation.Update));
        await audits.AddWithAudit(new AccountAuditRecordEntity { AccountId = accountId, AuditRecordId = audit.Id, CreatedAt = at }, audit, token);
    }
}
