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
    [InlineData("v4:0:0:0:0:0:0:0:0:0:2:0")]
    [InlineData("v4:0:0:0:0:0:0:0:0:0:0:3")]
    [InlineData("v4:0:0:0:0:0:0:0:0:0:0:0 ")]
    [InlineData("v4:0:0:0:0:0:0:0:0:8:0:0")]
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
    [InlineData("v1:5:3:2:1:0", "PQ-RANK-E")]
    [InlineData("v1:5:3:2:2:0", "PQ-RANK-D")]
    [InlineData("v1:5:3:2:3:0", "PQ-RANK-C")]
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
        var original = new Entitlement("PQ-CAT-A01", "Achievement", "A Quest of My Own", 1, 1, Rank.F, true);
        Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate("v2:5:7:7:1:0:2:2", [original]));
        AvatarAppearance.Validate("v2:5:7:7:1:0:2:2", [original], legacyCosmetics: true);
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

    /// <summary>Stage choices require exact rewards; revocation changes only the affected slot.</summary>
    /// <param name="code">The staged appearance.</param>
    /// <param name="rewardId">The exact current entitlement.</param>
    [Theory]
    [InlineData("v4:5:7:6:5:4:6:2:2:7:1:0", "PQ-CAT-B01")]
    [InlineData("v4:5:7:6:5:4:6:2:2:7:0:1", "PQ-CAT-A01")]
    [InlineData("v4:5:7:6:5:4:6:2:2:7:0:2", "PQ-CAT-F02")]
    public void StageChoicesRequireTheirCurrentEntitlement(string code, string rewardId)
    {
        const string starterStage = "v4:5:7:6:5:4:6:2:2:7:0:0";
        Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate(code, []));
        Entitlement reward = new(rewardId, "Fixture", "Reward", 10, 10, Rank.F, true);
        AvatarAppearance.Validate(code, [reward]);
        Assert.Equal(code, AvatarAppearance.Eligible(code, [reward]));
        Assert.Equal(starterStage, AvatarAppearance.Eligible(code, [reward with { Earned = false }]));
        Assert.Equal(starterStage, AvatarAppearance.Eligible(code, [reward with { Id = "different" }]));
        AvatarAppearance.Validate(starterStage, []);
    }

    /// <summary>Undo can revoke a scene independently while an earned pose and free identity survive.</summary>
    [Fact]
    public void StageRevocationIsIndependentAndDoesNotReequip()
    {
        Entitlement pose = new("PQ-CAT-B01", "Fixture", "Reward", 3, 3, Rank.F, true);
        const string saved = "v4:5:7:6:5:4:6:2:2:7:1:2";
        const string normalized = "v4:5:7:6:5:4:6:2:2:7:1:0";
        Assert.Equal(normalized, AvatarAppearance.Eligible(saved, [pose]));
        Entitlement scene = new("PQ-CAT-F02", "Fixture", "Reward", 10, 10, Rank.D, true);
        Assert.Equal(normalized, AvatarAppearance.Eligible(normalized, [pose, scene]));
    }

    /// <summary>All room displays require the exact current milestone and clear independently after revocation.</summary>
    /// <param name="decoration">The bounded decoration ordinal.</param>
    /// <param name="rewardId">The required current reward.</param>
    [Theory]
    [InlineData(1, "PQ-LEVEL-02")]
    [InlineData(2, "PQ-LEVEL-03")]
    [InlineData(3, "PQ-LEVEL-04")]
    [InlineData(4, "PQ-LEVEL-05")]
    [InlineData(5, "PQ-LEVEL-06")]
    [InlineData(6, "PQ-LEVEL-07")]
    public void RoomDisplaysUseCurrentEntitlements(int decoration, string rewardId)
    {
        Entitlement reward = new(rewardId, "Fixture", "Reward", 1, 1, Rank.F, true);
        for (var position = 1; position <= 4; position++)
        {
            var layout = "00000".ToCharArray();
            layout[position] = (char)('0' + decoration);
            var code = "v5:20000000000:" + new string(layout);
            Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate(code, []));
            AvatarAppearance.Validate(code, [reward]);
            Assert.Equal(code, AvatarAppearance.Eligible(code, [reward]));
            Assert.Equal("v5:20000000000:00000", AvatarAppearance.Eligible(code, [reward with { Earned = false }]));
        }
    }

    /// <summary>Room styles and their decorations revoke without changing the traveler's identity.</summary>
    [Fact]
    public void RoomsPreserveIdentityAndDoNotReequipAfterReearning()
    {
        const string code = "v5:57754622700:11000";
        Entitlement rank = new("PQ-RANK-E", "Achievement", "Rank E", 0, 0, Rank.E, true);
        Entitlement level = new("PQ-LEVEL-02", "Achievement", "Level 2", 2, 2, Rank.F, true);
        AvatarAppearance.Validate(code, [rank, level]);
        var normalized = AvatarAppearance.Eligible(code, []);
        Assert.Equal("v5:57754622700:00000", normalized);
        Assert.Equal(normalized, AvatarAppearance.Eligible(normalized, [rank, level]));
        AvatarAppearance.Validate("v5:20000000000:00000", []);
    }

    /// <summary>Reward reversal preserves the independent body type and build.</summary>
    [Fact]
    public void RevokingCosmeticsPreservesFemalePetite()
    {
        const string code = "v6:476106327011:11000";
        Entitlement achievement = new("PQ-CAT-A01", "Achievement", "A Quest of My Own", 1, 1, Rank.F, true);
        Entitlement rank = new("PQ-RANK-E", "Achievement", "Rank E", 0, 0, Rank.E, true);
        Entitlement level = new("PQ-LEVEL-02", "Achievement", "Level 2", 2, 2, Rank.F, true);
        AvatarAppearance.Validate(code, [achievement, rank, level]);
        Assert.Equal("v6:476006327001:00000", AvatarAppearance.Eligible(code, []));
        AvatarAppearance.Validate("v6:200000300000:00000", []);
        AvatarAppearance.Validate("v6:200000300201:00000", []);
    }

    /// <summary>Compact layout fields cannot exceed the database's bounded cosmetic ordinals.</summary>
    /// <param name="code">A malformed compact appearance.</param>
    [Theory]
    [InlineData("v5:20000000000:30000")]
    [InlineData("v5:20000000000:07000")]
    [InlineData("v5:20000000000:0000")]
    [InlineData("v5:60000000000:00000")]
    [InlineData("v5:20000000000:00000 ")]
    [InlineData("v6:200000400001:00000")]
    [InlineData("v6:200000300002:00000")]
    [InlineData("v6:200000300301:00000")]
    [InlineData("v6:200000300001:07000")]
    public void MalformedRoomChoicesAreRejected(string code) => Assert.Throws<QuestValidationException>(() => AvatarAppearance.Validate(code, []));
}
