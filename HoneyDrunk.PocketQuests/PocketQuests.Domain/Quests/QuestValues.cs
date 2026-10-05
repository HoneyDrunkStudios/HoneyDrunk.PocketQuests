using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PocketQuests.Domain.Quests;

internal static class QuestValues
{
    internal static Guid Derived(Guid accountId, string purpose) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"pq/projection-v1/{accountId:D}/{purpose}")).AsSpan(0, 16));

    internal static DateOnly? Date(string? value) => value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static TimeOnly? Time(string? value) => value is null ? null : TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);

    internal static (string? system, Guid? custom) Skill(string id) => Guid.TryParseExact(id, "D", out var custom) ? (null, custom) : (id, null);

    internal static string? DateText(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string? TimeText(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);
}
