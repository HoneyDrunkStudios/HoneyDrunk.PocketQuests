using NodaTime;
using PocketQuests.Contracts.Responses.Exports;
using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Schedules;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocketQuests.Services.Exports;

/// <summary>Private export serialization; no HTTP handling or retained files.</summary>
public static class ExportFormatting
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Serializes the unchanged JSON export values.</summary>
    /// <param name="snapshot">The coherent private snapshot.</param>
    /// <returns>UTF-8 JSON bytes.</returns>
    public static byte[] JsonBytes(QuestExport snapshot) => JsonSerializer.SerializeToUtf8Bytes(snapshot, Json);

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

    /// <summary>Builds the existing CSV archive entirely in memory.</summary>
    /// <param name="snapshot">The coherent private snapshot.</param>
    /// <returns>The ZIP archive bytes.</returns>
    public static byte[] CsvArchive(QuestExport snapshot)
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
