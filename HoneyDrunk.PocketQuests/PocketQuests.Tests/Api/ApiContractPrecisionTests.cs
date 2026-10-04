using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PocketQuests.Tests.Api;

/// <summary>Ensures endpoint-derived schemas distinguish accepted input from emitted output.</summary>
public sealed class ApiContractPrecisionTests
{
    /// <summary>Response fields are required and numeric, while input schemas preserve optional defaults and numeric strings.</summary>
    /// <returns>Completion of the generated schema assertions.</returns>
    [Fact]
    public async Task EndpointSchemasDescribeResponsePresenceAndDirectionalNumbers()
    {
        await using var host = new ApiApplicationFactory();
        using var client = host.CreateClient();
        var document = await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");
        var schemas = document!["components"]!["schemas"]!;
        foreach (var name in new[] { "Completion", "CustomSkill", "Occurrence", "OccurrenceLifecycle", "OccurrenceView", "PauseWindow", "PlayerProfile", "Quest", "QuestDefinition", "QuestExport", "QuestSeries", "QuestState", "ScheduleState", "SyncAnchor" })
        {
            var schema = schemas[name]!;
            var required = schema["required"]!.AsArray().Select(value => value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            Assert.All(schema["properties"]!.AsObject(), property => Assert.Contains(property.Key, required));
        }

        foreach (var (schema, property) in new[] { ("QuestState", "overallXp"), ("QuestState", "overallLevel"), ("Balance", "xp"), ("Balance", "level"), ("QuestDefinition", "revision"), ("Quest", "baseXp"), ("Quest", "penaltyPercent"), ("Share", "basisPoints"), ("QuestExport", "schemaVersion") })
        {
            var value = schemas[schema]!["properties"]![property]!;
            Assert.Equal("integer", value["type"]!.GetValue<string>());
            Assert.Null(value["anyOf"]);
        }

        foreach (var (schema, property) in new[] { ("QuestCommand", "expectedRevision"), ("QuestCommand", "interval"), ("QuestCommand", "acceptedLoss"), ("QuestInput", "penaltyPercent"), ("ShareInput", "basisPoints"), ("RecordedActionTime", "ordinal"), ("RecordedActionTime", "elapsedMilliseconds") })
        {
            var alternatives = schemas[schema]!["properties"]![property]!["anyOf"]!.AsArray();
            Assert.Contains(alternatives, option => option?["type"]?.GetValue<string>() == "string");
            Assert.Contains(alternatives, option => option?["type"]?.GetValue<string>() is "integer" or "number");
        }

        var inputRequired = schemas["QuestInput"]!["required"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.DoesNotContain("isCustom", inputRequired);
        Assert.DoesNotContain("description", inputRequired);
        Assert.DoesNotContain("penaltyPercent", inputRequired);
        Assert.DoesNotContain("baseXp", inputRequired);
        var definitionAlternatives = schemas["QuestCommand"]!["properties"]!["definition"]!["oneOf"]!.AsArray();
        Assert.Contains(definitionAlternatives, option => option?["$ref"]?.GetValue<string>() == "#/components/schemas/QuestInput");
    }
}
