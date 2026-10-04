using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Audit.Data;
using HoneyDrunk.Kernel.Abstractions.Identity;
using Microsoft.Data.SqlClient;
using PocketQuests.Application.Identity;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Projections;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Domain.Quests.Definitions;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Maps domain results to the single typed SQL command boundary.</summary>
public sealed partial class RelationalQuestCommands
{
    private static async Task Commit(Session session, AccountIdentity identity, AccountEntity account, QuestCommand command, byte[] digest, QuestAggregate aggregate, QuestState state, DateTimeOffset recordedAt, DateTimeOffset projectionAt, DateTimeOffset now, CancellationToken token)
    {
        var occurrence = command.Action == QuestActions.Accept ? aggregate.Occurrences[^1] : aggregate.Occurrences.Single(o => o.Id == command.OccurrenceId);
        var quest = occurrence.Quest;
        if (quest.IsCustom || quest.PenaltyPercent != 0)
            throw new NotSupportedException("Custom definition and penalty acceptance writers are not enabled in this lane.");
        await using var sql = session.Procedure("pocketquests.CommitQuestCommand");
        Add(sql, "@IdentityUserId", SqlDbType.VarChar, identity.Subject, 30);
        Add(sql, "@ExpectedVersion", SqlDbType.BigInt, account.MutationVersion);
        Add(sql, "@OperationId", SqlDbType.UniqueIdentifier, command.OperationId);
        Add(sql, "@Action", SqlDbType.VarChar, command.Action, 40);
        Add(sql, "@Digest", SqlDbType.Binary, digest, 32);
        Add(sql, "@Outcome", SqlDbType.NVarChar, JsonSerializer.Serialize(new CompactOutcome(projectionAt, state.CompletionOutcome)), -1);
        Add(sql, "@OccurrenceId", SqlDbType.UniqueIdentifier, occurrence.Id);
        Add(sql, "@CompletionId", SqlDbType.UniqueIdentifier, command.CompletionId);
        Add(sql, "@RecordedAt", SqlDbType.DateTimeOffset, recordedAt);
        Add(sql, "@ProjectionAt", SqlDbType.DateTimeOffset, projectionAt);
        Add(sql, "@AsOfDate", SqlDbType.Date, DateOnly.ParseExact(state.Today, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(sql, "@Now", SqlDbType.DateTimeOffset, now);
        Add(sql, "@AnchorId", SqlDbType.UniqueIdentifier, command.RecordedTime?.AnchorId);
        Add(sql, "@BootId", SqlDbType.UniqueIdentifier, command.RecordedTime?.BootId);
        Add(sql, "@Ordinal", SqlDbType.BigInt, command.RecordedTime?.Ordinal);
        Add(sql, "@Elapsed", SqlDbType.Float, command.RecordedTime?.ElapsedMilliseconds);
        Add(sql, "@SystemQuestId", SqlDbType.VarChar, quest.Id, 40);
        Add(sql, "@Title", SqlDbType.NVarChar, quest.Title, 120);
        Add(sql, "@Criterion", SqlDbType.NVarChar, quest.Criterion, 2000);
        Add(sql, "@Description", SqlDbType.NVarChar, quest.Description, 2000);
        Add(sql, "@CategoryId", SqlDbType.VarChar, quest.CategoryId, 40);
        Add(sql, "@RankCode", SqlDbType.VarChar, quest.Rank.ToString(), 1);
        Add(sql, "@EffortCode", SqlDbType.VarChar, quest.Effort.ToString(), 6);
        Add(sql, "@BaseXp", SqlDbType.BigInt, quest.BaseXp);
        var names = Catalog.Categories.Concat(Catalog.Attributes).Concat(Catalog.Skills).ToImmutableDictionary(i => i.Id, i => i.Name);
        Add(sql, "@DisplayLabels", SqlDbType.NVarChar, JsonSerializer.Serialize(new FrozenDisplay([.. quest.Attributes.Select(a => a.Id)], [.. quest.Skills.Select(s => s.Id)], names)), -1);
        Add(sql, "@DueOn", SqlDbType.Date, occurrence.DueDate is null ? null : DateOnly.ParseExact(occurrence.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(sql, "@PlannedTime", SqlDbType.Time, occurrence.PlannedTime is null ? null : TimeSpan.ParseExact(occurrence.PlannedTime, "hh\\:mm", CultureInfo.InvariantCulture));
        Add(sql, "@DeadlineAt", SqlDbType.DateTimeOffset, occurrence.Deadline);
        Tvp(sql, "@Attributes", "AllocationInput", Allocations(quest.Attributes));
        Tvp(sql, "@Skills", "AllocationInput", Allocations(quest.Skills));

        var ledger = Table(("Id", typeof(Guid)), ("EventId", typeof(Guid)), ("ContributionCode", typeof(string)), ("TrackCode", typeof(string)), ("TargetId", typeof(string)), ("EffectiveAt", typeof(DateTimeOffset)), ("Amount", typeof(long)));
        foreach (var entry in state.Ledger)
        {
            var completion = aggregate.Completions.Single(c => c.Id == entry.EventId);
            var baseXp = completion.Snapshot!.BaseXp;
            var baseAmount = entry.Track == "Category" ? baseXp : entry.Amount;
            Row("Base", baseAmount);
            if (entry.Track == "Category" && entry.Amount != baseAmount)
                Row("StreakBonus", entry.Amount - baseAmount);
            void Row(string contribution, long amount) => ledger.Rows.Add(
                Derived(account.Id, $"ledger/{entry.EventId:D}/{contribution}/{entry.Track}/{entry.TrackId}"),
                entry.EventId,
                contribution,
                entry.Track,
                entry.Track == "Overall" ? DBNull.Value : entry.TrackId,
                entry.At,
                amount);
        }

        Tvp(sql, "@Ledger", "LedgerInput", ledger);
        var balances = Table(("Id", typeof(Guid)), ("TrackCode", typeof(string)), ("TargetId", typeof(string)), ("EarnedXp", typeof(long)), ("Level", typeof(int)));
        balances.Rows.Add(Derived(account.Id, "balance/Overall"), "Overall", DBNull.Value, state.OverallXp, state.OverallLevel);
        foreach (var (track, values) in new[] { ("Category", values: state.Categories), ("Attribute", values: state.Attributes), ("Skill", values: state.Skills) })
            foreach (var value in values)
                balances.Rows.Add(Derived(account.Id, $"balance/{track}/{value.Id}"), track, value.Id, value.Xp, value.Level);
        Tvp(sql, "@Balances", "BalanceInput", balances);
        var streaks = Table(("Id", typeof(Guid)), ("CategoryId", typeof(string)), ("Days", typeof(int)), ("Bonus", typeof(int)), ("HasQualifiedToday", typeof(bool)));
        foreach (var value in state.Streaks)
            streaks.Rows.Add(Derived(account.Id, $"streak/{value.CategoryId}"), value.CategoryId, value.Days, value.Rate, value.QualifiedToday);
        Tvp(sql, "@Streaks", "StreakInput", streaks);
        var entitlements = Table(("Id", typeof(Guid)), ("RewardId", typeof(string)), ("QualifyingCount", typeof(int)), ("IsEarned", typeof(bool)));
        foreach (var value in state.Entitlements)
            entitlements.Rows.Add(Derived(account.Id, $"reward/{value.Id}"), value.Id, value.Count, value.Earned);
        Tvp(sql, "@Entitlements", "EntitlementInput", entitlements);

        var metadata = new Dictionary<string, string> { ["operationId"] = command.OperationId.ToString("N"), ["rulesetVersion"] = "1.0" };
        if (command.CompletionId is { } reversed)
            metadata["reversedCompletionId"] = reversed.ToString("N");
        var audit = AuditRecord.FromEntry(new AuditEntry(AuditEntryId.New(), now, identity.Subject, "pocketquests.quest." + command.Action, AuditCategory.UserActivity, AuditOutcome.Succeeded, new AuditTarget("quest.account", $"{account.Id:N}/{occurrence.Id:N}"), TenantId.Internal, Activity.Current?.TraceId.ToString(), command.Action == QuestActions.Accept ? AuditOperation.Create : AuditOperation.Update, Metadata: metadata));
        Add(sql, "@AuditId", SqlDbType.VarChar, audit.Id, 32);
        Add(sql, "@AuditCategory", SqlDbType.Int, (int)audit.Category);
        Add(sql, "@AuditOutcome", SqlDbType.Int, (int)audit.Outcome);
        Add(sql, "@AuditOperation", SqlDbType.Int, (int)audit.Operation);
        Add(sql, "@AuditTenant", SqlDbType.VarChar, audit.TenantId, 100);
        Add(sql, "@AuditCorrelation", SqlDbType.NVarChar, audit.CorrelationId, -1);
        Add(sql, "@AuditChanges", SqlDbType.NVarChar, audit.ChangesJson, -1);
        Add(sql, "@AuditMetadata", SqlDbType.NVarChar, audit.MetadataJson, -1);
        await sql.ExecuteNonQueryAsync(token);
    }

    private static Guid Derived(Guid account, string purpose) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"pq/projection-v1/{account:D}/{purpose}")).AsSpan(0, 16));

    private static DataTable Allocations(ImmutableArray<Share> shares)
    {
        var table = Table(("TargetId", typeof(string)), ("BasisPoints", typeof(int)));
        foreach (var share in shares)
            table.Rows.Add(share.Id, share.BasisPoints);
        return table;
    }

    private static DataTable Table(params (string name, Type type)[] columns)
    {
        var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        foreach (var (name, type) in columns)
            table.Columns.Add(name, type);
        return table;
    }

    private static void Tvp(SqlCommand sql, string parameterName, string type, DataTable value)
    {
        var parameter = sql.Parameters.Add(parameterName, SqlDbType.Structured);
        parameter.TypeName = "pocketquests." + type;
        parameter.Value = value;
    }
}
