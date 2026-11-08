using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Quests;
using PocketQuests.Domain.Progress;

namespace PocketQuests.Domain.Quests.Aggregates;

/// <summary>Account-owned character identity, independent of provider identity and earned progression.</summary>
public sealed partial class QuestAggregate
{
    private void SaveAvatar(QuestCommand command, DateTimeOffset now)
    {
        AvatarAppearance.Validate(command.AvatarCode, Project(now).Entitlements, LegacyCosmeticRewards);
        Profile = Profile with { AvatarCode = command.AvatarCode };
    }

    private void SaveCharacter(QuestCommand command)
    {
        var name = command.CharacterName?.Trim();
        Progression.Require(
            command.CharacterName is { Length: <= 40 } && !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsControl),
            "Character name: enter 1–40 characters without control characters.");
        Progression.Require(
            command.PortraitId is "portrait-1" or "portrait-2" or "portrait-3" or "portrait-4" or "portrait-5" or "portrait-6",
            "Portrait: choose one of the illustrated portraits.");
        Profile = Profile with { CharacterName = name, PortraitId = command.PortraitId };
    }
}
