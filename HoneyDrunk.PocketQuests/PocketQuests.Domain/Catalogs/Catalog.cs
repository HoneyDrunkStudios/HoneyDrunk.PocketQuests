using PocketQuests.Domain.Models.Catalogs;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Catalogs;

// IDs follow PRD list order, which also controls largest-remainder ties.

/// <summary>The approved starter catalog with stable IDs in PRD order.</summary>
public static class Catalog
{
    /// <summary>Gets the ten life categories.</summary>
    public static ImmutableArray<NamedItem> Categories { get; } = Names("c", [
        "Health & Fitness", "Learning", "Creativity", "Work & Purpose", "Relationships & Community",
        "Everyday Life", "Rest & Recreation", "Reflection & Spirituality", "Finances", "Exploration & Adventure"]);

    /// <summary>Gets the eight attributes in deterministic allocation order.</summary>
    public static ImmutableArray<NamedItem> Attributes { get; } = Names("a", [
        "Strength", "Endurance", "Flexibility", "Dexterity", "Intelligence", "Wisdom", "Creativity", "Charisma"]);

    /// <summary>Gets the eighteen skills in deterministic allocation order.</summary>
    public static ImmutableArray<NamedItem> Skills { get; } = Names("s", [
        "Strength Training", "Running", "Language Learning", "Research", "Drawing", "Creative Writing",
        "Programming", "Project Planning", "Communication", "Conflict Resolution", "Cooking", "Home Maintenance",
        "Meditation", "Journaling", "Budgeting", "Financial Planning", "Navigation", "Trip Planning"]);

    /// <summary>Gets the ten fixed starter quests and their immutable reward allocations.</summary>
    public static ImmutableArray<Quest> Quests { get; } = [
        Q(1, "Practice a familiar strength routine", "finish one strength-training session you already know and choose for yourself", [new("a01", 7500), new("a02", 2500)], [new("s01", Share.FullPoolBasisPoints)]),
        Q(2, "Return to a language", "finish one chosen practice exercise or session in a language you are learning", [new("a05", Share.FullPoolBasisPoints)], [new("s03", Share.FullPoolBasisPoints)]),
        Q(3, "Make a sketch", "finish one sketch of your choosing; no quality score", [new("a07", 7500), new("a04", 2500)], [new("s05", Share.FullPoolBasisPoints)]),
        Q(4, "Plan the next step", "identify and record for yourself one actionable next step for a project that matters to you", [new("a05", Share.FullPoolBasisPoints)], [new("s08", Share.FullPoolBasisPoints)]),
        Q(5, "Make time to listen", "take part in one mutually welcome conversation and listen attentively; no particular response from the other person is required", [new("a08", Share.FullPoolBasisPoints)], [new("s09", Share.FullPoolBasisPoints)]),
        Q(6, "Reset a household space", "finish cleaning or tidying one space you choose", [], []),
        Q(7, "Enjoy some downtime", "finish one chosen period of enjoying a game, TV, fiction, music or simply relaxing, for pleasure alone", [], []),
        Q(8, "Make space for reflection", "write one private reflection in a secular, spiritual or religious frame of your choice; never submit its contents", [new("a06", Share.FullPoolBasisPoints)], [new("s14", Share.FullPoolBasisPoints)]),
        Q(9, "Review your spending record", "finish reviewing and organizing one chosen set of your recent spending entries; no financial outcome or connected account required", [new("a05", Share.FullPoolBasisPoints)], [new("s15", Share.FullPoolBasisPoints)]),
        Q(10, "Discover somewhere local", "visit one unfamiliar local place you choose; no purchase, location tracking or proof upload", [], [])
    ];

    private static ImmutableArray<NamedItem> Names(string prefix, string[] names) =>
        names.Select((name, i) => new NamedItem($"{prefix}{i + 1:00}", name)).ToImmutableArray();

    private static Quest Q(int i, string title, string criterion, ImmutableArray<Share> attributes, ImmutableArray<Share> skills) =>
        new($"PQ-CAT-Q{i:00}", title, criterion, $"c{i:00}", Rank.F, Effort.Small, attributes, skills);
}
