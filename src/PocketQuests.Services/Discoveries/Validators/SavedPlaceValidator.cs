using PocketQuests.Contracts.Requests.Discoveries;
using PocketQuests.Domain.Errors;
using System.Text.RegularExpressions;

namespace PocketQuests.Services.Discoveries.Validators;

internal static partial class SavedPlaceValidator
{
    internal const int MaximumPlaces = 100;

    internal static void Validate(SavePlaceRequest request)
    {
        var category = request.Kind switch { "Parks" or "Gardens" => "c07", "Libraries" or "Museums" => "c02", "Art" => "c03", "Community" => "c05", "Markets" => "c06", "Viewpoints" => "c10", _ => null };
        if (request.Id == Guid.Empty || request.PlaceKey is null || !PlaceKey().IsMatch(request.PlaceKey) || request.PlaceKey.Length > 80 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 180 || request.Notes is null || request.Notes.Length > 1000 || category is null || category != request.CategoryId || request.Latitude is < -85 or > 85 || request.Longitude is < -180 or > 180 || request.ExpectedRevision is < 0 or int.MaxValue || request.QuestOccurrenceId == Guid.Empty)
            throw new QuestValidationException("Choose a supported place, valid coordinates and notes up to 1,000 characters.");
    }

    [GeneratedRegex(@"\A(node|way|relation)/[1-9][0-9]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceKey();
}
