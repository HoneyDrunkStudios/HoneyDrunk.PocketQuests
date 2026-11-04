using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;

namespace PocketQuests.Domain.Models.Accounts;

/// <summary>Backward-compatible appearance choices and cosmetic eligibility; never grants XP.</summary>
public static class AvatarAppearance
{
    /// <summary>The freely available starter appearance.</summary>
    public const string Starter = "v1:2:0:0:0:0";

    /// <summary>Validates the complete appearance and all selected reward cosmetics.</summary>
    /// <param name="code">The versioned appearance choice.</param>
    /// <param name="rewards">Server-calculated current entitlements.</param>
    public static void Validate(string? code, IEnumerable<Entitlement> rewards)
    {
        Progression.Require(IsValid(code), "Choose a supported avatar appearance.");
        Progression.Require(Eligible(code, rewards) == code, "Earn the clothing and accessories before saving this look.");
    }

    /// <summary>Preserves free choices and replaces revoked cosmetics with their starter options.</summary>
    /// <param name="code">The saved appearance; null preserves the starter default.</param>
    /// <param name="rewards">Server-calculated current entitlements.</param>
    /// <returns>The currently eligible appearance, without equipping re-earned cosmetics.</returns>
    public static string? Eligible(string? code, IEnumerable<Entitlement> rewards)
    {
        if (code is null)
            return null;
        Progression.Require(IsValid(code), "The saved avatar appearance is unsupported.");
        var earned = rewards.Where(reward => reward.Earned).Select(reward => reward.Id).ToHashSet(StringComparer.Ordinal);
        string? outfitReward = code[9] switch { '1' => "PQ-CAT-A01", '2' => "PQ-CAT-A02", '3' => "PQ-CAT-F01", _ => null };
        string? accessoryReward = code[11] switch { '1' => "PQ-CAT-B01", '2' => "PQ-CAT-B02", '3' => "PQ-CAT-F02", _ => null };
        var result = code.ToCharArray();
        if (outfitReward is not null && !earned.Contains(outfitReward))
            result[9] = '0';
        if (accessoryReward is not null && !earned.Contains(accessoryReward))
            result[11] = '0';
        return new(result);
    }

    private static bool IsValid(string? code) => IsLegacy(code) || IsExpanded(code) || IsLayered(code);

    private static bool IsLayered(string? code) => code is { Length: 20 } && code.StartsWith("v3:", StringComparison.Ordinal)
        && IsExpanded("v2:" + code[3..16])
        && code[16] == ':' && code[17] is >= '0' and <= '2'
        && code[18] == ':' && code[19] is >= '0' and <= '7';

    private static bool IsLegacy(string? code) => code is { Length: 12 } && code.StartsWith("v1:", StringComparison.Ordinal)
        && code[3] is >= '0' and <= '5' && code[4] == ':'
        && code[5] is >= '0' and <= '3' && code[6] == ':'
        && code[7] is >= '0' and <= '3' && code[8] == ':'
        && code[9] is >= '0' and <= '3' && code[10] == ':'
        && code[11] is >= '0' and <= '3';

    private static bool IsExpanded(string? code) => code is { Length: 16 } && code.StartsWith("v2:", StringComparison.Ordinal)
        && code[3] is >= '0' and <= '5' && code[4] == ':'
        && code[5] is >= '0' and <= '7' && code[6] == ':'
        && code[7] is >= '0' and <= '7' && code[8] == ':'
        && code[9] is >= '0' and <= '5' && code[10] == ':'
        && code[11] is >= '0' and <= '5' && code[12] == ':'
        && code[13] is >= '0' and <= '6' && code[14] == ':'
        && code[15] is >= '0' and <= '2';
}
