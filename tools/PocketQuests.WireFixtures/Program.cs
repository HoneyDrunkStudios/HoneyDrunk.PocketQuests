using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Quests.Aggregates;
using PocketQuests.Services.Catalogs;
using PocketQuests.Services.Projections.Mapping;
using System.Text.Json;
using System.Text.Json.Serialization;

var output = Path.GetFullPath(args.Single());
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
var at = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
var aggregate = new QuestAggregate("UTC");
var occurrenceId = Guid.Parse("00000000-0000-4000-8000-000000000001");
aggregate.Apply(new(Guid.Parse("00000000-0000-4000-8000-000000000002"), QuestActions.Accept, occurrenceId, Catalog.Quests[0].Id), at);
aggregate.Apply(new(Guid.Parse("00000000-0000-4000-8000-000000000003"), QuestActions.Complete, occurrenceId), at);
var fixtures = new
{
    State = aggregate.Project(at).ToModel(),
    Catalog = new CatalogService().Read(),
};
File.WriteAllText(Path.Combine(output, "wire-fixtures.json"), JsonSerializer.Serialize(fixtures, json) + "\n");
