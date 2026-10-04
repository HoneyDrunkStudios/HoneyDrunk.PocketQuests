using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PocketQuests.Data.Relational.Commands;

/// <summary>Collects typed source deltas and complete disposable projections for one atomic account command.</summary>
internal sealed class MutationBatch
{
    private readonly Dictionary<Type, object> _original = [];
    private readonly Dictionary<Type, object> _pending = [];

    internal static T Copy<T>(T value)
        where T : class => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    internal static bool Same<T>(T first, T second, params string[] ignored)
        where T : class => typeof(T).GetProperties().Where(p => p.Name is not ("CreatedAt" or "ModifiedAt" or "RowVersion") && !ignored.Contains(p.Name))
            .All(p => Equals(p.GetValue(first), p.GetValue(second)));

    internal async Task Load<T>(RelationalQuestReadContext db, Guid accountId, CancellationToken token)
        where T : class => Seed(await db.Set<T>().Where(row => EF.Property<Guid>(row, "AccountId") == accountId).ToListAsync(token));

    internal void Seed<T>(IEnumerable<T> rows)
        where T : class
    {
        _original[typeof(T)] = rows.ToDictionary(Key);
        _pending[typeof(T)] = new Dictionary<string, T>(StringComparer.Ordinal);
    }

    internal IEnumerable<T> Original<T>()
        where T : class => ((Dictionary<string, T>)_original[typeof(T)]).Values;

    internal IEnumerable<T> Rows<T>()
        where T : class => ((Dictionary<string, T>)_pending[typeof(T)]).Values;

    internal IEnumerable<T> All<T>()
        where T : class
    {
        var pending = (Dictionary<string, T>)_pending[typeof(T)];
        return pending.Values.Concat(((Dictionary<string, T>)_original[typeof(T)]).Where(pair => !pending.ContainsKey(pair.Key)).Select(pair => pair.Value));
    }

    internal T Put<T>(T row)
        where T : class
    {
        var key = Key(row);
        var original = (Dictionary<string, T>)_original[typeof(T)];
        if (original.TryGetValue(key, out var previous))
        {
            var created = typeof(T).GetProperty("CreatedAt");
            created?.SetValue(row, created.GetValue(previous));
            var modified = typeof(T).GetProperty("ModifiedAt");
            if (modified is not null && (DateTimeOffset)modified.GetValue(previous)! > (DateTimeOffset)modified.GetValue(row)!)
                modified.SetValue(row, modified.GetValue(previous));
            if (!MutationBindings.Replaces(typeof(T).Name[..^6]) && Same(previous, row))
                return previous;
        }

        ((Dictionary<string, T>)_pending[typeof(T)])[key] = row;
        return row;
    }

    private static string Key<T>(T row)
        where T : class => string.Join('/', Cache<T>.Keys.Select(p => Convert.ToString(p.GetValue(row), CultureInfo.InvariantCulture)));

    private static class Cache<T>
        where T : class
    {
        internal static readonly PropertyInfo[] Keys = [.. MutationBindings.Keys(typeof(T).Name[..^6]).Select(name => typeof(T).GetProperty(name)!)];
    }
}
