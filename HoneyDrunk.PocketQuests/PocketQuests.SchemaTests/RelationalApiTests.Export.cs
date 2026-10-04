using Microsoft.EntityFrameworkCore;
using PocketQuests.Application.Exports;
using PocketQuests.Data.Relational.Entities;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Progress;
using PocketQuests.Domain.Quests.Definitions;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Private version-one export content, history and CSV safety.</summary>
public sealed partial class RelationalApiTests
{
    /// <summary>JSON preserves historical terms and archived revisions; CSV preserves existing files and escapes formula text.</summary>
    /// <returns>Completion after HTTP downloads, source comparison and unchanged rowversion checks.</returns>
    [Fact]
    public async Task JsonAndCsvExportPreserveHistoricalTermsAndNeverWriteAccount()
    {
        using var host = new Host(fixture.Connection);
        using var client = host.Client();
        await Setup(client);
        var quest = new Quest(Guid.NewGuid().ToString("D"), "=original \"quoted\", title", "A concrete outcome", "c01", Rank.F, Effort.Small, [new("a01", 10000)], [], true, "Description");
        await Command(client, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest, ExpectedRevision: 0));
        var accept = new QuestCommand(Guid.NewGuid(), QuestActions.Accept, Guid.NewGuid(), QuestId: quest.Id);
        await Command(client, accept);
        var complete = new QuestCommand(Guid.NewGuid(), QuestActions.Complete, accept.OccurrenceId);
        await Command(client, complete);
        await Command(client, new(Guid.NewGuid(), QuestActions.Undo, accept.OccurrenceId, CompletionId: complete.OperationId));
        var edited = quest with { Title = "Later title" };
        await Command(client, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: edited, ExpectedRevision: 1));
        await Command(client, new(Guid.NewGuid(), QuestActions.ArchiveDefinition, QuestId: quest.Id, ExpectedRevision: 2));
        using var bob = host.Client(NewOwner());
        await Setup(bob);
        await Command(bob, new(Guid.NewGuid(), QuestActions.SaveDefinition, Definition: quest with { Id = Guid.NewGuid().ToString("D"), Title = "Other owner's secret" }, ExpectedRevision: 0));
        await using var db = fixture.Context();
        var before = await db.Set<AccountEntity>().SingleAsync(a => a.IdentityUserId == host.Owner);
        using var response = await client.GetAsync(new Uri("/api/export/json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        var text = await response.Content.ReadAsStringAsync();
        var export = JsonSerializer.Deserialize<QuestExport>(text, Json)!;
        Assert.Equal(1, export.SchemaVersion);
        Assert.Equal(Start, export.GeneratedAt);
        Assert.Equal(Start, export.SnapshotCutoff);
        Assert.Equal(before.Id, export.AccountId);
        Assert.Equal(3, export.DefinitionRevisions.Length);
        Assert.Equal(new[] { 1, 2, 3 }, export.DefinitionRevisions.Select(r => r.Revision));
        Assert.Equal(new[] { false, false, true }, export.DefinitionRevisions.Select(r => r.Archived));
        Assert.Equal(new[] { quest.Title, edited.Title, edited.Title }, export.DefinitionRevisions.Select(r => r.Quest.Title));
        Assert.Equal(quest.Title, Assert.Single(export.Completions).Snapshot!.Title);
        Assert.Equal(complete.OperationId, Assert.Single(export.Undos).CompletionId);
        Assert.Equal(JsonSerializer.Serialize(await Read(client), Json), JsonSerializer.Serialize(export.State, Json));
        Assert.DoesNotContain("Other owner", text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Owner, text, StringComparison.Ordinal);
        using var csv = await client.GetAsync(new Uri("/api/export/csv", UriKind.Relative));
        csv.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", csv.Content.Headers.ContentType!.MediaType);
        Assert.True(csv.Headers.CacheControl!.NoStore);
        using var archive = new ZipArchive(new MemoryStream(await csv.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
        Assert.Equal(new[] { "snapshot.txt", "occurrences.csv", "completions.csv", "undo.csv", "ledger.csv" }, archive.Entries.Select(e => e.FullName));
        using var reader = new StreamReader(archive.GetEntry("completions.csv")!.Open());
        var rows = await reader.ReadToEndAsync();
        Assert.Contains("\"'=original \"\"quoted\"\", title\"", rows, StringComparison.Ordinal);
        Assert.Contains(complete.OperationId.ToString(), rows, StringComparison.Ordinal);
        Assert.Equal(before.RowVersion, (await db.Set<AccountEntity>().SingleAsync(a => a.Id == before.Id)).RowVersion);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(new Uri("/api/export/xml", UriKind.Relative))).StatusCode);
    }
}
