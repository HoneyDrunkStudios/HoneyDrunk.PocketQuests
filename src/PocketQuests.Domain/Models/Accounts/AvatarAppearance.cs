using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Progress;

namespace PocketQuests.Domain.Models.Accounts;

/// <summary>Backward-compatible appearance choices and cosmetic eligibility; never grants XP.</summary>
public static class AvatarAppearance
{
    /// <summary>The freely available starter appearance.</summary>
    public const string Starter = "v1:2:0:0:0:0";

    private static readonly string[] LegacyDecorationRewards = [string.Empty, "PQ-CAT-A01", "PQ-CAT-A02", "PQ-CAT-F01", "PQ-CAT-B01", "PQ-CAT-B02", "PQ-CAT-F02"];
    private static readonly string[] LevelDecorationRewards = [string.Empty, "PQ-LEVEL-02", "PQ-LEVEL-03", "PQ-LEVEL-04", "PQ-LEVEL-05", "PQ-LEVEL-06", "PQ-LEVEL-07"];

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
        var outfitReward = code[outfit] switch
        {
            '1' => legacyCosmetics ? "PQ-CAT-A01" : "PQ-RANK-E",
            '2' => legacyCosmetics ? "PQ-CAT-A02" : "PQ-RANK-D",
            '3' => legacyCosmetics ? "PQ-CAT-F01" : "PQ-RANK-C",
            _ => null,
        };
        ClearLocked(result, outfit, outfitReward, earned);
        ClearLocked(result, accessory, code[accessory] switch { '1' => "PQ-CAT-B01", '2' => "PQ-CAT-B02", '3' => "PQ-CAT-F02", _ => null }, earned);
        if (compact || code[1] == '4')
        {
            var pose = compact ? 12 : 21;
            var backdrop = compact ? 13 : 23;
            ClearLocked(result, pose, code[pose] == '1' ? "PQ-CAT-B01" : null, earned);
            ClearLocked(result, backdrop, code[backdrop] switch { '1' => "PQ-CAT-A01", '2' => "PQ-CAT-F02", _ => null }, earned);
        }

        if (compact)
        {
            var room = code[1] == '6' ? 16 : 15;
            var roomReward = code[room] switch
            {
                '1' => legacyCosmetics ? "PQ-CAT-A01" : "PQ-RANK-E",
                '2' => legacyCosmetics ? "PQ-CAT-F02" : "PQ-RANK-D",
                _ => null,
            };
            ClearLocked(result, room, roomReward, earned);
            var decorationRewards = legacyCosmetics ? LegacyDecorationRewards : LevelDecorationRewards;
            for (var index = room + 1; index < result.Length; index++)
            {
                if (code[index] != '0')
                    ClearLocked(result, index, decorationRewards[code[index] - '0'], earned);
            }
        }

        return new(result);
    }

    private static void ClearLocked(char[] code, int index, string? reward, HashSet<string> earned)
    {
        if (reward is not null && !earned.Contains(reward))
            code[index] = '0';
    }

    private static bool IsValid(string? code) => IsLegacy(code) || IsExpanded(code) || IsLayered(code) || IsStaged(code) || IsCompact(code);

    private static bool IsCompact(string? code)
    {
        if (code is not { Length: 20 or 21 })
            return false;
        var hasBodyType = code.Length == 21;
        var room = hasBodyType ? 16 : 15;
        return code.StartsWith(hasBodyType ? "v6:" : "v5:", StringComparison.Ordinal)
            && code[3] is >= '0' and <= '5' && code[4] is >= '0' and <= '7'
            && code[5] is >= '0' and <= '7' && code[6] is >= '0' and <= '5'
            && code[7] is >= '0' and <= '5' && code[8] is >= '0' and <= '6'
            && code[9] >= '0' && code[9] <= (hasBodyType ? '3' : '2')
            && code[10] is >= '0' and <= '2' && code[11] is >= '0' and <= '7'
            && code[12] >= '0' && code[12] <= (hasBodyType ? '2' : '1')
            && code[13] is >= '0' and <= '2'
            && (!hasBodyType || code[14] is >= '0' and <= '1')
            && code[room - 1] == ':' && code[room] is >= '0' and <= '2'
            && code[(room + 1)..].All(value => value is >= '0' and <= '6');
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
