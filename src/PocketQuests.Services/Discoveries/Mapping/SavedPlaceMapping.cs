using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Data.Entities.Discoveries;

namespace PocketQuests.Services.Discoveries.Mapping;

internal static class SavedPlaceMapping
{
    internal static void Apply(SavePlaceRequest request, SavedPlaceEntity row)
    {
        row.Name = request.Name.Trim();
        row.Kind = request.Kind;
        row.CategoryId = request.CategoryId;
        row.Latitude = decimal.Round(request.Latitude, 6);
        row.Longitude = decimal.Round(request.Longitude, 6);
        row.Notes = request.Notes;
        row.QuestOccurrenceId = request.QuestOccurrenceId;
    }

    internal static PocketQuests.Contracts.Models.Discoveries.SavedPlace ToContract(this SavedPlaceEntity row) => new(row.Id, row.PlaceKey, row.Name, row.Kind, row.CategoryId, row.Latitude, row.Longitude, row.Notes, row.VisitedAt, row.QuestOccurrenceId, row.Revision, row.CreatedAt, row.ModifiedAt);

    internal static PocketQuests.Domain.Models.Discoveries.SavedPlace ToModel(this SavedPlaceEntity row) => new(row.Id, row.PlaceKey, row.Name, row.Kind, row.CategoryId, row.Latitude, row.Longitude, row.Notes, row.VisitedAt, row.QuestOccurrenceId, row.Revision, row.CreatedAt, row.ModifiedAt);
}
