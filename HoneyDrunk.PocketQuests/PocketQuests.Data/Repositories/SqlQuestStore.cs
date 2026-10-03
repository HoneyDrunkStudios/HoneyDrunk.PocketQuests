using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Exports;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Application.Synchronization;
using PocketQuests.Data.Context;
using PocketQuests.Data.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Profiles;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using PocketQuests.Domain.Quests.Events;
using PocketQuests.Domain.Quests.Occurrences;
using PocketQuests.Domain.Schedules;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PocketQuests.Data.Repositories;

/// <summary>Serializes account changes with SQL transaction locks and atomic command receipts.</summary>
public sealed partial class SqlQuestStore(QuestDbContext db) : IQuestStore, ISyncAnchors
{
    /// <inheritdoc />
    public async Task<QuestState> Read(AccountIdentity identity, string initialZone, DateTimeOffset now, CancellationToken token) =>
        (await Transact(identity, initialZone, null, now, token)).state;

    /// <inheritdoc />
    public async Task<QuestState> Execute(AccountIdentity identity, QuestCommand command, DateTimeOffset now, CancellationToken token) =>
        (await Transact(identity, null, command, now, token)).state;

    /// <inheritdoc />
    public async Task<QuestExport> Export(AccountIdentity identity, DateTimeOffset now, CancellationToken token) =>
        (await Transact(identity, null, null, now, token, exporting: true)).export!;

    // A validated anchor may lead receipt time by up to five seconds. Live state must
    // include committed events at that logical time, including immediate reads/replay.
    // Keep the recorded timestamp and domain deadline/Undo validation unchanged.
    private static DateTimeOffset CommittedProjectionTime(QuestAggregate aggregate, DateTimeOffset now) =>
        aggregate.Completions.Select(c => c.RecordedAt).Concat(aggregate.Undos.Select(u => u.RecordedAt)).Append(now).Max();

    private async Task<(QuestState state, QuestExport? export)> Transact(AccountIdentity identity, string? initialZone, QuestCommand? command, DateTimeOffset now, CancellationToken token, bool exporting = false, SyncAnchorEntity? issuingAnchor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Subject);
        var identityKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(identity))));
        await using var transaction = await db.Database.BeginTransactionAsync(token);

        // SQL-owned transaction lock serializes one account even across API processes,
        // including its first creation. It is released automatically on rollback/crash.
        var resource = "pocketquests:" + identityKey;
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Account transaction is busy.', 1;", token);
        if (await db.Erasures.AnyAsync(m => m.UserId == identity.Subject, token)
            || await db.LifecycleBarriers.AnyAsync(b => b.UserId == identity.Subject && b.State != IdentityProtocol.Active, token))
            throw new UnauthorizedAccessException("This account is inactive or erased.");
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.IdentityKey == identityKey, token);
        if (account is null)
        {
            if (initialZone is null)
                throw new InvalidOperationException("Finish account setup first.");
            account = new() { Id = Guid.NewGuid(), IdentityKey = identityKey, Zone = Scheduling.Zone(initialZone).Id, CreatedAt = now };
            db.Accounts.Add(account);
            await db.SaveChangesAsync(token);
        }

        var payload = command is null ? null : JsonSerializer.Serialize(command);
        if (command is not null)
        {
            var receipt = await db.Operations.SingleOrDefaultAsync(x => x.AccountId == account.Id && x.Id == command.OperationId, token);
            if (receipt is not null)
            {
                if (JsonSerializer.Serialize(JsonSerializer.Deserialize<QuestCommand>(receipt.Payload)) != payload)
                    throw new InvalidOperationException("This operation ID was already used for different details.");
                var saved = JsonSerializer.Deserialize<QuestState>(receipt.Result)!;
                return (saved with {
                    Definitions = saved.Definitions.IsDefault ? [] : saved.Definitions,
                    Profile = saved.Profile ?? PlayerProfile.Empty,
                    Schedule = saved.Schedule ?? ScheduleState.Empty,
                    Penalties = saved.Penalties.IsDefault ? [] : saved.Penalties,
                    Ledger = saved.Ledger.IsDefault ? [] : saved.Ledger }, null);
            }
        }

        var occurrences = await db.Occurrences.Where(x => x.AccountId == account.Id).AsNoTracking().ToListAsync(token);
        var completions = await db.Completions.Where(x => x.AccountId == account.Id).AsNoTracking().ToListAsync(token);
        var undos = await db.Undos.Where(x => x.AccountId == account.Id).AsNoTracking().ToListAsync(token);
        var definitions = await db.Definitions.Where(x => x.AccountId == account.Id).ToListAsync(token);
        var aggregate = new QuestAggregate(
            account.Zone,
            occurrences.Select(o => new Occurrence(o.Id, JsonSerializer.Deserialize<Quest>(o.QuestSnapshot)!, o.DueDate, o.Deadline, o.AcceptedAt, o.PlannedTime, o.ParentId, o.Lifecycle is null ? null : JsonSerializer.Deserialize<OccurrenceLifecycle>(o.Lifecycle))),
            completions.Select(c => new Completion(c.Id, c.OccurrenceId, c.RecordedAt, c.QuestSnapshot is null ? null : JsonSerializer.Deserialize<Quest>(c.QuestSnapshot))),
            undos.Select(u => new UndoEvent(u.Id, u.CompletionId, u.RecordedAt)),
            definitions.Select(d => JsonSerializer.Deserialize<QuestDefinition>(d.Document)!),
            account.Profile is null ? null : JsonSerializer.Deserialize<PlayerProfile>(account.Profile),
            account.Schedule is null ? null : JsonSerializer.Deserialize<ScheduleState>(account.Schedule));
        aggregate.Reconcile(now);
        QuestState? beforeCompletion = null;
        if (command is not null)
        {
            var recordedAt = await RecordedAt(account.Id, command, aggregate, now, token);
            if (command.Action == QuestActions.Complete)
                beforeCompletion = aggregate.Project(CommittedProjectionTime(aggregate, recordedAt > now ? recordedAt : now));
            aggregate.Apply(command, recordedAt);
        }

        var state = aggregate.Project(CommittedProjectionTime(aggregate, now));
        if (beforeCompletion is not null && command?.OccurrenceId is { } completedOccurrence)
            state = state with { CompletionOutcome = CompletionOutcome.Between(beforeCompletion, state, command.OperationId, completedOccurrence) };
        if (issuingAnchor is not null)
        {
            issuingAnchor.AccountId = account.Id;
            issuingAnchor.Snapshot = JsonSerializer.Serialize(state.Occurrences);
            db.SyncAnchors.Add(issuingAnchor);
        }

        account.Zone = state.Zone;
        account.Profile = JsonSerializer.Serialize(state.Profile);
        account.Schedule = JsonSerializer.Serialize(state.Schedule);
        foreach (var definition in aggregate.Definitions)
        {
            var row = definitions.SingleOrDefault(d => d.Id == definition.Quest.Id);
            if (row is null)
            {
                row = new() { AccountId = account.Id, Id = definition.Quest.Id };
                db.Definitions.Add(row);
            }

            var document = JsonSerializer.Serialize(definition);
            if (row.Document != document)
                db.DefinitionRevisions.Add(new() { AccountId = account.Id, DefinitionId = definition.Quest.Id, Revision = definition.Revision, Document = document });
            row.Document = document;
        }

        foreach (var o in aggregate.Occurrences)
        {
            var row = occurrences.SingleOrDefault(x => x.Id == o.Id);
            if (row is null)
            {
                row = new() { AccountId = account.Id, Id = o.Id, AcceptedAt = o.AcceptedAt };
                db.Occurrences.Add(row);
            }
            else
            {
                db.Occurrences.Attach(row);
            }

            row.QuestSnapshot = JsonSerializer.Serialize(o.Quest);
            row.DueDate = o.DueDate;
            row.Deadline = o.Deadline;
            row.PlannedTime = o.PlannedTime;
            row.ParentId = o.ParentId;
            row.Lifecycle = JsonSerializer.Serialize(o.Lifecycle);
        }

        foreach (var c in aggregate.Completions.Skip(completions.Count))
            db.Completions.Add(new() { AccountId = account.Id, Id = c.Id, OccurrenceId = c.OccurrenceId, RecordedAt = c.RecordedAt, QuestSnapshot = JsonSerializer.Serialize(c.Snapshot) });
        foreach (var u in aggregate.Undos.Skip(undos.Count))
            db.Undos.Add(new() { AccountId = account.Id, Id = u.Id, CompletionId = u.CompletionId, RecordedAt = u.RecordedAt });
        if (command is not null)
        {
            var changed = db.ChangeTracker.HasChanges() || aggregate.Occurrences.Count != occurrences.Count
                || aggregate.Completions.Count != completions.Count
                || aggregate.Undos.Count != undos.Count;
            db.Operations.Add(new() { AccountId = account.Id, Id = command.OperationId, Payload = payload!, Result = JsonSerializer.Serialize(state) });
            if (changed)
            {
                // Persist the canonical envelope directly in this unit's transaction;
                // an independent writer must not commit audit ahead of the quest ledger.
                var targetId = command.OccurrenceId?.ToString("N") ?? command.Definition?.Id ?? command.QuestId ?? account.Id.ToString("N");
                var metadata = new Dictionary<string, string>
                {
                    ["operationId"] = command.OperationId.ToString("N"),
                    ["rulesetVersion"] = "1.0",
                };
                if (command.CompletionId is { } completionId)
                    metadata["reversedCompletionId"] = completionId.ToString("N");
                db.Audit.Add(AuditRecord.FromEntry(new AuditEntry(
                    AuditEntryId.New(),
                    now,
                    identity.Subject,
                    "pocketquests.quest." + command.Action,
                    AuditCategory.UserActivity,
                    AuditOutcome.Succeeded,
                    new AuditTarget("quest.account", $"{account.Id:N}/{targetId}"),
                    TenantId.Internal,
                    Activity.Current?.TraceId.ToString(),
                    command.Action == QuestActions.Accept ? AuditOperation.Create : AuditOperation.Update,
                    Metadata: metadata)));
            }
        }

        await db.SaveChangesAsync(token);
        QuestExport? snapshot = null;
        if (exporting)
        {
            var revisions = await db.DefinitionRevisions.Where(r => r.AccountId == account.Id).AsNoTracking().OrderBy(r => r.DefinitionId).ThenBy(r => r.Revision).ToListAsync(token);
            snapshot = new(1, now, now, account.Id, state, [.. revisions.Select(r => JsonSerializer.Deserialize<QuestDefinition>(r.Document)!)], [.. aggregate.Completions], [.. aggregate.Undos]);
        }

        await transaction.CommitAsync(token);
        return (state, snapshot);
    }
}
