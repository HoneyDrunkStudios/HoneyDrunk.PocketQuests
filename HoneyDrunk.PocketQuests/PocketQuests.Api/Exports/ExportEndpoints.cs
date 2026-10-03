using NodaTime;
using PocketQuests.Application.Exports;
using PocketQuests.Application.Identity;
using PocketQuests.Application.Persistence;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Schedules;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Api.Exports;

/// <summary>Private on-demand exports without retained artifacts, public links or credentials.</summary>
public static class ExportEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Registers authenticated consistent-snapshot downloads.</summary>
    /// <param name="app">The endpoint host.</param>
    public static void MapExportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/export/{format}", async (string format, HttpContext context, IQuestStore store, TimeProvider clock, CancellationToken token) =>
        {
            if (format is not "json" and not "csv")
                return Results.BadRequest();
            var identity = new AccountIdentity(context.User.FindFirstValue("iss")!, context.User.FindFirstValue("sub")!);
            var snapshot = await store.Export(identity, clock.GetUtcNow(), token);
            context.Response.Headers.CacheControl = "no-store, private";
            return format == "json"
                ? Results.File(JsonSerializer.SerializeToUtf8Bytes(snapshot, Json), "application/json", "pocket-quests.json")
                : Results.File(CsvArchive(snapshot), "application/zip", "pocket-quests-csv.zip");
        }).RequireAuthorization();
    }

    /// <summary>Quotes CSV cells and neutralizes formula-leading user text while JSON preserves originals.</summary>
    /// <param name="value">The value being exported.</param>
    /// <returns>A quoted, escaped cell.</returns>
    public static string CsvCell(string? value)
    {
        value ??= string.Empty;
        var trimmed = value.TrimStart();
        if ((trimmed.Length > 0 && "=+-@".Contains(trimmed[0], StringComparison.Ordinal)) || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static byte[] CsvArchive(QuestExport snapshot)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write("snapshot.txt", $"Snapshot cutoff: {snapshot.SnapshotCutoff:O}\n{snapshot.Notice}");
            Write("occurrences.csv", Rows(
                ["occurrenceId", "definitionId", "title", "criterion", "category", "categoryName", "status", "dueDate", "plannedTime", "deadlineUtc", "deadlineLocal", "zone", "parentId", "baseXp", "lockedLoss", "actualLoss", "attributes", "skills"],
                snapshot.State.Occurrences.Select(o => new[] { o.Occurrence.Id.ToString(), o.Occurrence.Quest.Id, o.Occurrence.Quest.Title, o.Occurrence.Quest.Criterion, o.Occurrence.Quest.CategoryId, Catalog.Categories.Single(c => c.Id == o.Occurrence.Quest.CategoryId).Name, o.Status.ToString(), o.Occurrence.DueDate, o.Occurrence.PlannedTime, o.Occurrence.Deadline?.ToString("O", CultureInfo.InvariantCulture), o.Occurrence.Deadline is { } deadline ? LocalTime(deadline) : null, snapshot.State.Zone, o.Occurrence.ParentId?.ToString(), o.Occurrence.Quest.BaseXp.ToString(CultureInfo.InvariantCulture), o.Occurrence.Lifecycle?.LockedLoss?.ToString(CultureInfo.InvariantCulture), snapshot.State.Penalties.SingleOrDefault(p => p.OccurrenceId == o.Occurrence.Id)?.ActualLoss.ToString(CultureInfo.InvariantCulture), JsonSerializer.Serialize(o.Occurrence.Quest.Attributes, Json), JsonSerializer.Serialize(o.Occurrence.Quest.Skills, Json) })));
            Write("completions.csv", Rows(["completionId", "occurrenceId", "recordedAtUtc", "recordedAtLocal", "zone", "title", "categoryName", "snapshot"], snapshot.Completions.Select(c => new[] { c.Id.ToString(), c.OccurrenceId.ToString(), c.RecordedAt.ToString("O", CultureInfo.InvariantCulture), LocalTime(c.RecordedAt), snapshot.State.Zone, c.Snapshot?.Title, Catalog.Categories.SingleOrDefault(category => category.Id == c.Snapshot?.CategoryId)?.Name, JsonSerializer.Serialize(c.Snapshot, Json) })));
            Write("undo.csv", Rows(["undoId", "completionId", "recordedAtUtc", "recordedAtLocal", "zone"], snapshot.Undos.Select(u => new[] { u.Id.ToString(), u.CompletionId.ToString(), u.RecordedAt.ToString("O", CultureInfo.InvariantCulture), LocalTime(u.RecordedAt), snapshot.State.Zone })));
            Write("ledger.csv", Rows(["eventId", "occurrenceId", "effectiveAtUtc", "effectiveAtLocal", "zone", "track", "trackId", "trackName", "xp"], snapshot.State.Ledger.Select(e => new[] { e.EventId.ToString(), e.OccurrenceId.ToString(), e.At.ToString("O", CultureInfo.InvariantCulture), LocalTime(e.At), snapshot.State.Zone, e.Track, e.TrackId, TrackName(e.TrackId), e.Amount.ToString(CultureInfo.InvariantCulture) })));

            string LocalTime(DateTimeOffset at) => Instant.FromDateTimeOffset(at).InZone(Scheduling.Zone(snapshot.State.Zone)).ToDateTimeOffset().ToString("O", CultureInfo.InvariantCulture);

            string TrackName(string id) => snapshot.State.Categories.Concat(snapshot.State.Attributes).Concat(snapshot.State.Skills).FirstOrDefault(b => b.Id == id)?.Name ?? "Overall";

            void Write(string name, string content)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static string Rows(string[] headings, IEnumerable<string?[]> rows) =>
        string.Join(',', headings.Select(CsvCell)) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(',', row.Select(CsvCell)))) + "\r\n";
}
