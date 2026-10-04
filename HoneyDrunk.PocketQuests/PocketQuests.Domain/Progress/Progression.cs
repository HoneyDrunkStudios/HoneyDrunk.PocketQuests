using PocketQuests.Domain.Catalogs;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Models.Skills;
using System.Collections.Immutable;

namespace PocketQuests.Domain.Progress;

/// <summary>Pure calibrated progression rules; no client or persistence dependencies.</summary>
public static class Progression
{
    /// <summary>The maximum category-only streak bonus percentage.</summary>
    public const int MaximumStreakBonusPercent = 20;

    private const int RankMultiplierScale = 10;
    private const int PercentDenominator = 100;
    private const int HalfPercentDenominator = PercentDenominator / 2;
    private static readonly ImmutableArray<int> RewardMultipliersInTenths = [10, 13, 17, 22, 28, 35, 43];
    private static readonly ImmutableArray<int> BaseXpByEffort = [10, 80, 800];
    private static readonly ImmutableArray<int> MinimumLevelByRank = [1, 5, 10, 20, 35, 50, 70];

    /// <summary>Gets the global-rank breadth and total gates.</summary>
    public static ImmutableArray<RankRule> Rules { get; } = [
        new(Rank.F, 0, 0, 0), new(Rank.E, 2, 70, 300), new(Rank.D, 3, 250, 1800), new(Rank.C, 4, 600, 6000),
        new(Rank.B, 5, 1300, 16000), new(Rank.A, 7, 3000, 45000), new(Rank.S, 10, 9000, 150000)];

    /// <summary>Calculates the whole-number base reward for rank and effort.</summary>
    /// <param name="rank">The rank whose calibrated rule applies.</param>
    /// <param name="effort">The accepted effort tier.</param>
    /// <returns>Base XP before category streak bonuses.</returns>
    public static int Reward(Rank rank, Effort effort)
    {
        Require(Enum.IsDefined(rank) && Enum.IsDefined(effort), "Invalid rank or effort.");
        return BaseXpByEffort[(int)effort] * RewardMultipliersInTenths[(int)rank] / RankMultiplierScale;
    }

    /// <summary>Calculates cumulative XP at the start of a level.</summary>
    /// <param name="level">A one-based level.</param>
    /// <param name="track">The progression curve to use.</param>
    /// <returns>The checked integer threshold.</returns>
    public static long Threshold(int level, Track track)
    {
        Require(level >= 1, "Level must be positive.");
        return checked(Coefficient(track) * (long)(level - 1) * (level - 1));
    }

    /// <summary>Calculates level from nonnegative XP using integer boundary correction.</summary>
    /// <param name="xp">Nonnegative whole-number XP.</param>
    /// <param name="track">The progression curve to use.</param>
    /// <returns>The one-based level.</returns>
    public static int Level(long xp, Track track)
    {
        Require(xp >= 0, "XP cannot be negative.");

        // Integer comparisons correct floating-point rounding at large thresholds.
        var k = Coefficient(track);
        long root = (long)Math.Sqrt(xp / k);
        while ((root + 1) <= (xp / k) / (root + 1))
            root++;
        while (root > 0 && root > (xp / k) / root)
            root--;
        return checked((int)(root + 1));
    }

    /// <summary>Looks up the approved onboarding skill seed.</summary>
    /// <param name="experience">The selected onboarding tier.</param>
    /// <returns>Skill XP only; this does not grant category or overall XP.</returns>
    public static long Seed(Experience experience) => experience switch
    {
        Experience.New => 0,
        Experience.Practiced => 405,
        Experience.Experienced => 5780,
        Experience.Expert => 12005,
        _ => throw new QuestValidationException("Invalid experience.")
    };

    /// <summary>Checks every selected skill gate, or the category gate when no skills are selected.</summary>
    /// <param name="quest">Accepted quest terms.</param>
    /// <param name="categories">Current category XP by stable ID.</param>
    /// <param name="skills">Current skill XP by stable ID.</param>
    /// <returns>Whether current progress meets the quest rank gate.</returns>
    public static bool Eligible(Quest quest, IReadOnlyDictionary<string, long> categories, IReadOnlyDictionary<string, long> skills)
    {
        Require(Enum.IsDefined(quest.Rank), "Invalid rank.");
        var gate = MinimumLevelByRank[(int)quest.Rank];
        return quest.Skills.IsEmpty
            ? Level(categories.GetValueOrDefault(quest.CategoryId), Track.Category) >= gate
            : quest.Skills.All(s => Level(skills.GetValueOrDefault(s.Id), Track.Skill) >= gate);
    }

    /// <summary>Divides one reward pool by largest remainder, breaking ties by stable ID.</summary>
    /// <param name="xp">Nonnegative whole-number XP.</param>
    /// <param name="input">Recipient shares totaling ten thousand basis points.</param>
    /// <returns>Whole-number allocations whose sum equals the input pool, or empty when no recipients exist.</returns>
    public static ImmutableDictionary<string, int> Allocate(int xp, IEnumerable<Share> input)
    {
        Require(xp >= 0, "XP cannot be negative.");
        var shares = input.ToArray();
        if (shares.Length == 0)
            return ImmutableDictionary<string, int>.Empty;
        Require(
            shares.All(s => !string.IsNullOrWhiteSpace(s.Id) && s.BasisPoints is >= 0 and <= Share.FullPoolBasisPoints)
            && shares.Sum(s => (long)s.BasisPoints) == Share.FullPoolBasisPoints
            && shares.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() == shares.Length,
            "Allocations must have unique IDs and total 100%.");
        var result = shares.ToDictionary(s => s.Id, s => (int)((long)xp * s.BasisPoints / Share.FullPoolBasisPoints), StringComparer.Ordinal);
        var remaining = xp - result.Values.Sum();
        foreach (var s in shares.OrderByDescending(s => ((long)xp * s.BasisPoints) % Share.FullPoolBasisPoints).ThenBy(s => s.Id, StringComparer.Ordinal).Take(remaining))
            result[s.Id]++;
        return result.ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Evaluates both breadth gates against all ten category balances.</summary>
    /// <param name="categories">Current category XP by stable ID.</param>
    /// <returns>The highest qualifying rank and next-rank progress.</returns>
    public static RankProgress GlobalRank(IReadOnlyDictionary<string, long> categories)
    {
        Require(categories.Keys.All(id => Catalog.Categories.Any(c => c.Id == id)) && categories.Values.All(x => x >= 0), "Invalid category balances.");
        var balances = Catalog.Categories.Select(c => categories.GetValueOrDefault(c.Id)).ToArray();
        var total = balances.Sum();
        var rank = Rules.Last(r => total >= r.Total && balances.Count(x => x >= r.Floor) >= r.Count).Rank;
        var next = Rules[Math.Min((int)rank + 1, (int)Rank.S)];
        return new(rank, next, balances.Count(x => x >= next.Floor), total);
    }

    /// <summary>Recomputes catalog rewards from surviving completions and current rank.</summary>
    /// <param name="survivingOccurrences">Quest snapshots for completions that have not been reversed.</param>
    /// <param name="rank">The rank whose calibrated rule applies.</param>
    /// <returns>All reward definitions with current eligibility.</returns>
    public static ImmutableArray<Entitlement> Entitlements(IEnumerable<Quest> survivingOccurrences, Rank rank)
    {
        var quests = survivingOccurrences.ToArray();
        return [
            E("A01", "Achievement", "A Quest of My Own", quests.Count(q => q.IsCustom), 1, Rank.F),
            E("A02", "Achievement", "Room to Recharge", Count("c07"), 5, Rank.F),
            E("B01", "Badge", "Curious Explorer", Count("c10"), 3, Rank.F),
            E("B02", "Badge", "Everyday Care", Count("c06"), 10, Rank.F),
            E("F01", "Frame", "Open Notebook", Count("c02"), 10, Rank.E),
            E("F02", "Frame", "Quiet Horizon", Count("c07"), 10, Rank.D)];
        int Count(string id) => quests.Count(q => q.CategoryId == id);
        Entitlement E(string id, string kind, string name, int count, int required, Rank minimum) =>
            new($"PQ-CAT-{id}", kind, name, count, required, minimum, count >= required && rank >= minimum);
    }

    /// <summary>Calculates the rounded category-only bonus, capped at twenty percent.</summary>
    /// <param name="baseXp">The occurrence's base reward.</param>
    /// <param name="day">One-based streak day.</param>
    /// <returns>The whole-number bonus XP.</returns>
    public static int StreakBonus(int baseXp, int day) => checked(((baseXp * Math.Clamp(day - 1, 0, MaximumStreakBonusPercent)) + HalfPercentDenominator) / PercentDenominator);

    internal static void Require(bool valid, string message)
    {
        if (!valid)
            throw new QuestValidationException(message);
    }

    private static long Coefficient(Track track) => track switch
    {
        Track.Overall => 100,
        Track.Category => 10,
        Track.Attribute => 25,
        Track.Skill => 5,
        _ => throw new QuestValidationException("Invalid track.")
    };
}
