using PocketQuests.Domain.Catalogs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PocketQuests.Tests.Api;

/// <summary>Observes JSON presence and numeric token kinds through the actual API Program.</summary>
public sealed class ApiSerializationTests
{
    /// <summary>Numeric strings remain accepted inputs while successful state responses contain JSON numbers.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task ActualProgramAcceptsNumericStringsAndWritesNumbers()
    {
        await using var host = new ApiApplicationFactory();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-a");
        var quest = JsonSerializer.SerializeToNode(Catalog.Quests[0], new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } })!;
        quest["penaltyPercent"] = "0";
        quest["baseXp"] = "12345";
        quest["attributes"]![0]!["basisPoints"] = "10000";
        var input = new JsonObject
        {
            ["operationId"] = Guid.NewGuid().ToString(),
            ["action"] = "serialization-test",
            ["expectedRevision"] = "2",
            ["interval"] = "3",
            ["acceptedLoss"] = "4",
            ["definition"] = quest,
            ["recordedTime"] = new JsonObject
            {
                ["anchorId"] = Guid.NewGuid().ToString(),
                ["bootId"] = Guid.NewGuid().ToString(),
                ["ordinal"] = "5",
                ["elapsedMilliseconds"] = "1200.5",
                ["deviceUtc"] = ApiTestStore.At,
            },
        };
        using var content = new StringContent(input.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/commands", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, host.Store.LastCommand!.ExpectedRevision);
        Assert.Equal(3, host.Store.LastCommand.Interval);
        Assert.Equal(4, host.Store.LastCommand.AcceptedLoss);
        Assert.Equal(5, host.Store.LastCommand.RecordedTime!.Ordinal);
        Assert.Equal(1200.5, host.Store.LastCommand.RecordedTime.ElapsedMilliseconds);
        Assert.Equal(10000, host.Store.LastCommand.Definition!.Attributes[0].BasisPoints);
        Assert.Equal(Catalog.Quests[0].BaseXp, host.Store.LastCommand.Definition.BaseXp);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var state = json.RootElement;
        Assert.Equal(JsonValueKind.Number, state.GetProperty("overallXp").ValueKind);
        Assert.Equal(JsonValueKind.Number, state.GetProperty("overallLevel").ValueKind);
        Assert.Equal(JsonValueKind.Number, state.GetProperty("categories")[0].GetProperty("xp").ValueKind);
        Assert.Equal(JsonValueKind.Number, state.GetProperty("categories")[0].GetProperty("level").ValueKind);
        Assert.Equal(JsonValueKind.Number, state.GetProperty("definitions")[0].GetProperty("revision").ValueKind);
        Assert.Equal(JsonValueKind.Number, state.GetProperty("occurrences")[0].GetProperty("occurrence").GetProperty("quest").GetProperty("baseXp").ValueKind);
    }

    /// <summary>All 44 disputed response fields are present, including explicit null and default values.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task ActualProgramEmitsDefaultsAndNullResponseProperties()
    {
        await using var host = new ApiApplicationFactory();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-a");
        using var response = await client.GetAsync(new Uri("/api/state", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var state = document.RootElement;
        var view = state.GetProperty("occurrences")[0];
        var occurrence = view.GetProperty("occurrence");
        var lifecycle = occurrence.GetProperty("lifecycle");
        var profile = state.GetProperty("profile");
        var schedule = state.GetProperty("schedule");
        Present(view.GetProperty("completion"), "snapshot");
        Present(profile.GetProperty("customSkills")[0], "archived", "revision");
        Present(occurrence, "lifecycle", "parentId", "plannedTime");
        Present(lifecycle, "abandonedAt", "deadlineZone", "frozenAt", "individuallyFrozen", "lockedLoss", "lossCategoryId", "scheduleVersion", "sequence", "seriesId", "sourceAnchorId", "unaccepted");
        Present(view, "planned");
        Present(schedule.GetProperty("pauses")[0], "endedAt");
        Present(profile, "assessmentHistory", "badgeId", "customSkills", "expiryWarnings", "frameId", "onboardingComplete", "zoneHistory");
        Present(occurrence.GetProperty("quest"), "baseXp", "description", "isCustom", "penaltyPercent");
        Present(state.GetProperty("definitions")[0], "archived", "revision");
        Present(schedule.GetProperty("series")[0], "autoAcceptPenalty", "effectiveAt", "nextSequence", "pauseDays", "plannedTime", "stopped", "version");
        Present(state, "completionOutcome", "futureWarnings");
        Present(schedule, "accountPaused");
        Assert.Equal(JsonValueKind.Null, occurrence.GetProperty("parentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, occurrence.GetProperty("plannedTime").ValueKind);
        Assert.Equal(JsonValueKind.Null, lifecycle.GetProperty("abandonedAt").ValueKind);
        Assert.Equal(JsonValueKind.False, lifecycle.GetProperty("unaccepted").ValueKind);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("badgeId").ValueKind);
        Assert.Equal(JsonValueKind.False, profile.GetProperty("expiryWarnings").ValueKind);
        Assert.Equal(1, profile.GetProperty("customSkills")[0].GetProperty("revision").GetInt32());
        Assert.Equal(JsonValueKind.False, schedule.GetProperty("accountPaused").ValueKind);
        using var export = await client.GetAsync(new Uri("/api/export/json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var exported = JsonDocument.Parse(await export.Content.ReadAsStringAsync());
        Present(exported.RootElement, "notice");
        Assert.Equal(JsonValueKind.Number, exported.RootElement.GetProperty("schemaVersion").ValueKind);
        using var anchor = await client.PostAsJsonAsync("/api/sync-anchor", new { deviceId = Guid.NewGuid(), bootId = Guid.NewGuid(), deviceUtc = ApiTestStore.At });
        Assert.Equal(HttpStatusCode.OK, anchor.StatusCode);
        using var anchored = JsonDocument.Parse(await anchor.Content.ReadAsStringAsync());
        Present(anchored.RootElement, "recordedTimeFloor");
        Assert.Equal(JsonValueKind.Null, anchored.RootElement.GetProperty("recordedTimeFloor").ValueKind);
    }

    /// <summary>Optional command and nested quest input defaults still bind when omitted.</summary>
    /// <returns>Completion of the HTTP assertions.</returns>
    [Fact]
    public async Task ActualProgramPreservesOptionalRequestDefaults()
    {
        await using var host = new ApiApplicationFactory();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-a");
        var quest = JsonSerializer.SerializeToNode(Catalog.Quests[0], new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } })!.AsObject();
        foreach (var property in new[] { "isCustom", "description", "penaltyPercent", "baseXp" })
            quest.Remove(property);
        var command = new JsonObject { ["operationId"] = Guid.NewGuid().ToString(), ["action"] = "serialization-test", ["definition"] = quest };
        using var content = new StringContent(command.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/commands", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(host.Store.LastCommand!.ExpectedRevision);
        Assert.False(host.Store.LastCommand.ConfirmPenalty);
        Assert.False(host.Store.LastCommand.Definition!.IsCustom);
        Assert.Null(host.Store.LastCommand.Definition.Description);
        Assert.Equal(0, host.Store.LastCommand.Definition.PenaltyPercent);
        Assert.Equal(Catalog.Quests[0].BaseXp, host.Store.LastCommand.Definition.BaseXp);
    }

    private static void Present(JsonElement value, params string[] properties)
    {
        foreach (var property in properties)
            Assert.True(value.TryGetProperty(property, out _), $"Expected response property {property} was omitted.");
    }
}
