using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Progress;

namespace PocketQuests.Data.Configurations.Progress;

/// <summary>Maps ProfileReward fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class ProfileRewardMapping : IEntityTypeConfiguration<ProfileRewardEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProfileRewardEntity> entity)
    {
        entity.ToTable("ProfileReward", "pocketquests", table =>
        {
            table.HasComment("One row is one public achievement, badge or frame definition. Classification: Public. History: reference; Retain referenced reward codes; history is the versioned catalog. Not erased with accounts.");
            table.HasCheckConstraint("CK_ProfileReward_KindCode", "[KindCode] IN ('Achievement','Badge','Frame')");
            table.HasCheckConstraint("CK_ProfileReward_RequiredRank", "[RequiredRank] IN ('F','E','D','C','B','A','S')");
            table.HasCheckConstraint("CK_ProfileReward_RequiredCount", "[RequiredCount] >= 0");
            table.HasCheckConstraint("CK_ProfileReward_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_ProfileReward_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_ProfileReward_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_ProfileReward").IsClustered();
        entity.HasAlternateKey(row => new { row.Id, row.KindCode }).HasName("UQ_ProfileReward_Id_KindCode");
        entity.Property(row => row.Id).HasComment("Stable reward code, including existing A01/A02/B01/B02/F01/F02 IDs.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).ValueGeneratedNever();
        entity.Property(row => row.KindCode).HasComment("Achievement, Badge or Frame; only Badge and Frame are selectable.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false);
        entity.Property(row => row.Name).HasComment("Public display name of this reward.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(120).IsUnicode(true);
        entity.Property(row => row.RequiredCount).HasComment("Nonnegative qualifying count in the versioned reward rule.");
        entity.Property(row => row.RequiredRank).HasColumnType("char(1)").HasComment("Minimum global rank in the versioned reward rule.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(1).IsUnicode(false);
        entity.Property(row => row.RulesetVersion).HasComment("Reviewed catalog/ruleset version that defines the qualifying predicate.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
    }
}
