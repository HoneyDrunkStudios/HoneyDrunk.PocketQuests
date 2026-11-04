using PocketQuests.Domain.Commands;
using PocketQuests.Domain.Errors;
using PocketQuests.Domain.Models.Accounts;
using PocketQuests.Domain.Models.Progress;
using PocketQuests.Domain.Quests.Aggregates;

namespace PocketQuests.Tests.Unit;

/// <summary>Avatar validation and reward gating preserve progression and free identity choices.</summary>
public sealed class AvatarTests
{
    /// <summary>Rejects malformed, unknown and missing appearance versions.</summary>
    /// <param name="code">The unsupported appearance.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v2:2:0:0:0:0")]
    [InlineData("v1:6:0:0:0:0")]
    [InlineData("v1:2:4:0:0:0")]
    [InlineData("v1:2:0:4:0:0")]
    [InlineData("v1:2:0:0:4:0")]
    [InlineData("v1:2:0:0:0:4")]
    [InlineData("v1:2:0:0:0:0 ")]
    [InlineData("v3:2:0:0:0:0:0:0")]
    [InlineData("v2:6:0:0:0:0:0:0")]
    [InlineData("v2:0:8:0:0:0:0:0")]
    [InlineData("v2:0:0:8:0:0:0:0")]
    [InlineData("v2:0:0:0:6:0:0:0")]
    [InlineData("v2:0:0:0:0:6:0:0")]
    [InlineData("v2:0:0:0:0:0:7:0")]
    [InlineData("v2:0:0:0:0:0:0:3")]
    [InlineData("v2:0:0:0:0:0:0:0 ")]
    [InlineData("v3:0:0:0:0:0:0:0:3:0")]
    [InlineData("v3:0:0:0:0:0:0:0:0:8")]
    [InlineData("v3:0:0:0:0:0:0:0:0:0 ")]
    public void InvalidAppearanceIsRejected(string? code) => Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate(code, []));

    /// <summary>Every reward cosmetic requires its exact entitlement; revocation preserves free choices.</summary>
    /// <param name="code">The outfit or accessory preview.</param>
    /// <param name="rewardId">The matching reward.</param>
    [Theory]
    [InlineData("v1:5:3:2:1:0", "PQ-CAT-A01")]
    [InlineData("v1:5:3:2:2:0", "PQ-CAT-A02")]
    [InlineData("v1:5:3:2:3:0", "PQ-CAT-F01")]
    [InlineData("v1:5:3:2:0:1", "PQ-CAT-B01")]
    [InlineData("v1:5:3:2:0:2", "PQ-CAT-B02")]
    [InlineData("v1:5:3:2:0:3", "PQ-CAT-F02")]
    public void CosmeticsFollowExactEntitlements(string code, string rewardId)
    {
        Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate(code, []));
        Entitlement reward = new(rewardId, "Fixture", "Reward", 10, 10, Rank.F, true);
        AvatarAppearance.Validate(code, [reward]);
        Assert.Equal(code, AvatarAppearance.Eligible(code, [reward]));
        Assert.Equal("v1:5:3:2:0:0", AvatarAppearance.Eligible(code, [reward with { Earned = false }]));
        Assert.Equal("v1:5:3:2:0:0", AvatarAppearance.Eligible(code, [reward with { Id = "another-reward" }]));
    }

    /// <summary>Saving free appearance never changes XP and cannot be smuggled into a different command.</summary>
    [Fact]
    public void FreeAppearanceChangesNoProgression()
    {
        var aggregate = new QuestAggregate("UTC");
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var before = aggregate.Project(now);
        aggregate.Apply(new(Guid.NewGuid(), QuestActions.SaveAvatar, AvatarCode: "v1:0:3:3:0:0"), now);
        var after = aggregate.Project(now);
        Assert.Equal("v1:0:3:3:0:0", after.Profile.AvatarCode);
        Assert.Equal(before.OverallXp, after.OverallXp);
        Assert.Equal(before.Ledger, after.Ledger);
        Assert.Throws<QuestValidationException>(() => aggregate.Apply(new(Guid.NewGuid(), QuestActions.FinishOnboarding, AvatarCode: AvatarAppearance.Starter), now));
    }

    /// <summary>Expanded free identity and starter gear remain free across every face and build.</summary>
    [Fact]
    public void ExpandedIdentityAndStarterWardrobeAreFree()
    {
        for (var face = 0; face < 7; face++)
        {
            for (var build = 0; build < 3; build++)
            {
                var code = $"v2:5:7:7:5:5:{face}:{build}";
                AvatarAppearance.Validate(code, []);
                Assert.Equal(code, AvatarAppearance.Eligible(code, []));
            }
        }
    }

    /// <summary>Undo strips only unavailable reward slots and retains all expanded identity fields.</summary>
    [Fact]
    public void ExpandedRevocationPreservesIdentityAndFreeGear()
    {
        Assert.Equal("v2:5:7:6:0:5:2:1", AvatarAppearance.Eligible("v2:5:7:6:3:5:2:1", []));
        Assert.Equal("v2:4:6:7:4:0:1:2", AvatarAppearance.Eligible("v2:4:6:7:4:3:1:2", []));
        Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate("v2:5:7:7:1:0:2:2", []));
        AvatarAppearance.Validate("v2:5:7:7:1:0:2:2", [new("PQ-CAT-A01", "Fixture", "Reward", 1, 1, Rank.F, true)]);
    }

    /// <summary>Layered starter accessories and scarf colors survive revocation of reward gear.</summary>
    [Fact]
    public void LayeredAccessoriesPreserveLegacyAppearanceAndOwnership()
    {
        for (var extra = 0; extra < 3; extra++)
        {
            for (var color = 0; color < 8; color++)
            {
                var code = $"v3:5:7:7:5:4:6:2:{extra}:{color}";
                AvatarAppearance.Validate(code, []);
                Assert.Equal(code, AvatarAppearance.Eligible(code, []));
            }
        }

        Assert.Equal("v3:5:7:6:0:0:6:2:2:7", AvatarAppearance.Eligible("v3:5:7:6:3:1:6:2:2:7", []));
        Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate("v3:5:7:6:3:1:6:2:2:7", []));
    }
}
