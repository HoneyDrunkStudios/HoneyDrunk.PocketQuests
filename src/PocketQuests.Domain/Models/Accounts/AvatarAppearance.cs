using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;

namespace PocketQuests.Domain.Models.Accounts;

/// <summary>Backward-compatible appearance choices and cosmetic eligibility; never grants XP.</summary>
public static class AvatarAppearance
{
    /// <summary>The freely available starter appearance.</summary>
    public const string Starter = "v1:2:0:0:0:0";

    private const string OwnQuestReward = "PQ-CAT-A01";
    private const string RechargeReward = "PQ-CAT-F02";
    private const string PoseReward = "PQ-CAT-B01";
    private static readonly string[] LegacyOutfitRewards = [string.Empty, OwnQuestReward, "PQ-CAT-A02", "PQ-CAT-F01"];
    private static readonly string[] LegacyDecorationRewards = [string.Empty, OwnQuestReward, "PQ-CAT-A02", "PQ-CAT-F01", PoseReward, "PQ-CAT-B02", RechargeReward];
    private static readonly string[] LevelDecorationRewards = [string.Empty, "PQ-LEVEL-02", "PQ-LEVEL-03", "PQ-LEVEL-04", "PQ-LEVEL-05", "PQ-LEVEL-06", "PQ-LEVEL-07"];
    private static readonly string[] RankOutfitRewards = [string.Empty, "PQ-RANK-E", "PQ-RANK-D", "PQ-RANK-C"];
    private static readonly string[] RankRoomRewards = [string.Empty, "PQ-RANK-E", "PQ-RANK-D"];
    private static readonly string[] LegacySceneRewards = [string.Empty, OwnQuestReward, RechargeReward];
    private static readonly string[] AccessoryRewards = [string.Empty, PoseReward, "PQ-CAT-B02", RechargeReward];

    /// <summary>Validates the complete appearance and all selected reward cosmetics.</summary>
    /// <param name="code">The versioned appearance choice.</param>
    /// <param name="rewards">Server-calculated current entitlements.</param>
    /// <param name="legacyCosmetics">Whether to apply the original gates when replaying a historical command.</param>
    public static void Validate(string? code, IEnumerable<Entitlement> rewards, bool legacyCosmetics = false)
    {
        Progression.Require(IsValid(code), "Choose a supported avatar appearance.");
        Progression.Require(Eligible(code, rewards, legacyCosmetics) == code, "Earn the clothing, accessories, poses, backgrounds, rooms and decorations before saving this look.");
    }

    /// <summary>Preserves free choices and replaces revoked cosmetics with their starter options.</summary>
    /// <param name="code">The saved appearance; null preserves the starter default.</param>
    /// <param name="rewards">Server-calculated current entitlements.</param>
    /// <param name="legacyCosmetics">Whether to apply the original gates when replaying a historical command.</param>
    /// <returns>The currently eligible appearance, without equipping re-earned cosmetics.</returns>
    public static string? Eligible(string? code, IEnumerable<Entitlement> rewards, bool legacyCosmetics = false)
    {
        if (code is null)
            return null;
        Progression.Require(IsValid(code), "The saved avatar appearance is unsupported.");
        var earned = rewards.Where(reward => reward.Earned).Select(reward => reward.Id).ToHashSet(StringComparer.Ordinal);
        var result = code.ToCharArray();
        var compact = code[1] is '5' or '6';
        var outfit = compact ? 6 : 9;
        var accessory = compact ? 7 : 11;
        ClearLocked(result, outfit, legacyCosmetics ? LegacyOutfitRewards : RankOutfitRewards, earned);
        ClearLocked(result, accessory, AccessoryRewards, earned);
        if (compact || code[1] == '4')
            ApplyStageRewards(result, compact, earned);

        if (compact)
            ApplyRoomRewards(result, earned, legacyCosmetics);

        return new(result);
    }

    private static void ApplyStageRewards(char[] code, bool compact, HashSet<string> earned)
    {
        var pose = compact ? 12 : 21;
        var backdrop = compact ? 13 : 23;
        if (code[pose] == '1' && !earned.Contains(PoseReward))
            code[pose] = '0';
        ClearLocked(code, backdrop, LegacySceneRewards, earned);
    }

    private static void ApplyRoomRewards(char[] code, HashSet<string> earned, bool legacyCosmetics)
    {
        var room = code[1] == '6' ? 16 : 15;
        ClearLocked(code, room, legacyCosmetics ? LegacySceneRewards : RankRoomRewards, earned);
        var decorationRewards = legacyCosmetics ? LegacyDecorationRewards : LevelDecorationRewards;
        for (var index = room + 1; index < code.Length; index++)
            ClearLocked(code, index, decorationRewards, earned);
    }

    private static void ClearLocked(char[] code, int index, string[] rewards, HashSet<string> earned)
    {
        var selected = code[index] - '0';
        if (selected > 0 && selected < rewards.Length && !earned.Contains(rewards[selected]))
            code[index] = '0';
    }

    private static bool IsValid(string? code) => IsLegacy(code) || IsExpanded(code) || IsLayered(code) || IsStaged(code) || IsCompact(code);

    // Limits follow the compact fields in order, from skin through backdrop (v5) or body type (v6).
    private static bool IsCompact(string? code) => code?.Length switch
    {
        20 => IsCompact(code, "v5:", "57755622712"),
        21 => IsCompact(code, "v6:", "577556327221"),
        _ => false,
    };

    private static bool IsCompact(string code, string prefix, string limits)
    {
        var room = limits.Length + 4;
        return code.StartsWith(prefix, StringComparison.Ordinal)
            && WithinDigitRanges(code.AsSpan(3, limits.Length), limits)
            && code[room - 1] == ':'
            && WithinDigitRanges(code.AsSpan(room), "26666");
    }

    private static bool WithinDigitRanges(ReadOnlySpan<char> digits, ReadOnlySpan<char> limits)
    {
        for (var index = 0; index < digits.Length; index++)
        {
            if (digits[index] < '0' || digits[index] > limits[index])
                return false;
        }

        return true;
    }

    private static bool IsStaged(string? code) => code is { Length: 24 } && code.StartsWith("v4:", StringComparison.Ordinal)
        && IsLayered("v3:" + code[3..20])
        && code[20] == ':' && code[21] is >= '0' and <= '1'
        && code[22] == ':' && code[23] is >= '0' and <= '2';

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
