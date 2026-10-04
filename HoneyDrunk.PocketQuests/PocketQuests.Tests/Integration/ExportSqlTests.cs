using PocketQuests.Api.Exports;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;

namespace PocketQuests.Tests.Integration;

/// <summary>Private exports preserve revision history while isolating owners and neutralizing CSV formulas.</summary>
public sealed partial class SqlApiTests
{
    /// <summary>Verifies authenticated JSON and CSV exports from the actual SQL account snapshot.</summary>
    /// <returns>The completed export regression.</returns>
    [Fact]
    public async Task ExportsArePrivateConsistentAndFormulaSafe()
    {
        using var host = new Host(Connection);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        using var anonymous = host.CreateClient();
        await Setup(alice);
        await Setup(bob);
        var quest = new Quest(Guid.NewGuid().ToString(), "=HYPERLINK(unsafe)", "Line one, \"quoted\"\nLine two", "c07", Rank.F, Effort.Small, [], [], true);
        await Command(alice, new(Guid.NewGuid(), "save-definition", Definition: quest, ExpectedRevision: 0));
        var state = await Command(alice, new(Guid.NewGuid(), "accept", QuestId: quest.Id));
        await Command(alice, new(Guid.NewGuid(), "complete", state.Occurrences.Single().Occurrence.Id));
        await Command(alice, new(Guid.NewGuid(), "save-definition", Definition: quest with { Title = "Revision two" }, ExpectedRevision: 1));
        var response = await alice.GetAsync(new Uri("/api/export/json", UriKind.Relative));
        Assert.True(response.Headers.CacheControl!.NoStore);
        var export = (await response.Content.ReadFromJsonAsync<QuestExport>(Json))!;
        Assert.Equal(export.SnapshotCutoff, export.GeneratedAt);
        Assert.Equal(2, export.DefinitionRevisions.Length);
        Assert.Equal("=HYPERLINK(unsafe)", export.Completions.Single().Snapshot!.Title);
        Assert.Equal(10, export.State.Ledger.Where(e => e.Track == "Overall").Sum(e => e.Amount));
        Assert.DoesNotContain("Bearer", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("IdentityKey", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var other = (await bob.GetFromJsonAsync<QuestExport>("/api/export/json", Json))!;
        Assert.NotEqual(export.AccountId, other.AccountId);
        Assert.Empty(other.State.Occurrences);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/export/json", UriKind.Relative))).StatusCode);
        using var csv = await alice.GetStreamAsync(new Uri("/api/export/csv", UriKind.Relative));
        using var archive = new ZipArchive(csv);
        using var reader = new StreamReader(archive.GetEntry("occurrences.csv")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.Contains("'=HYPERLINK(unsafe)", text, StringComparison.Ordinal);
        Assert.Contains("\"\"quoted\"\"", text, StringComparison.Ordinal);
    }

    /// <summary>Checks formula and whitespace edge cases without modifying JSON source strings.</summary>
    /// <param name="input">Unsafe spreadsheet-leading content.</param>
    [Theory]
    [InlineData("=1+1")]
    [InlineData(" +SUM(A1:A2)")]
    [InlineData("@name")]
    [InlineData("\tformula")]
    [InlineData("\rformula")]
    public void CsvFormulaLeadingCellsAreNeutralized(string input) => Assert.StartsWith("\"'", ExportEndpoints.CsvCell(input), StringComparison.Ordinal);
}
