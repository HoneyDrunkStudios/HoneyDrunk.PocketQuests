using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Attributes;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Configurations.Progress;

/// <summary>Maps XpBalance fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class XpBalanceMapping : IEntityTypeConfiguration<XpBalanceEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<XpBalanceEntity> entity)
    {
        entity.ToTable("XpBalance", "pocketquests", table =>
        {
            table.HasComment("One row is one current account/track-target projection with earned and seed components separated. Classification: Restricted. History: projection; Current projection only; rebuild from history and erase with account. No temporal copies.");
            table.HasCheckConstraint("CK_XpBalance_TrackTarget", "([TrackCode]='Overall' AND [CategoryId] IS NULL AND [AttributeId] IS NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Category' AND [CategoryId] IS NOT NULL AND [AttributeId] IS NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Attribute' AND [CategoryId] IS NULL AND [AttributeId] IS NOT NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Skill' AND [CategoryId] IS NULL AND [AttributeId] IS NULL AND (([SystemSkillId] IS NOT NULL AND [CustomSkillId] IS NULL) OR ([SystemSkillId] IS NULL AND [CustomSkillId] IS NOT NULL)))");
            table.HasCheckConstraint("CK_XpBalance_EarnedXp", "[EarnedXp] >= 0");
            table.HasCheckConstraint("CK_XpBalance_SeedXp", "[SeedXp] >= 0");
            table.HasCheckConstraint("CK_XpBalance_Level", "[Level] >= 1");
            table.HasCheckConstraint("CK_XpBalance_ProjectionVersion", "[ProjectionVersion] >= 0");
            table.HasCheckConstraint("CK_XpBalance_SeedPool", "[TrackCode]='Skill' OR [SeedXp]=0");
            table.HasCheckConstraint("CK_XpBalance_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_XpBalance_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_XpBalance_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_XpBalance").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_XpBalance_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.TrackCode, row.CategoryId, row.AttributeId, row.SystemSkillId, row.CustomSkillId }, "UQ_XpBalance_TrackTarget").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.TrackCode).HasColumnType("varchar(12)").HasComment("Progression pool: Overall, Category, Attribute or Skill.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Category target; null unless TrackCode is Category.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.AttributeId).HasColumnType("varchar(40)").HasComment("Attribute target; null unless TrackCode is Attribute.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SystemSkillId).HasColumnType("varchar(40)").HasComment("System skill target; null for other pools or a custom skill.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CustomSkillId).HasColumnType("uniqueidentifier").HasComment("Account-owned custom skill target; null for other pools or a system skill.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.EarnedXp).HasColumnType("bigint").HasComment("Nonnegative earned balance after chronological zero-floor processing.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.SeedXp).HasColumnType("bigint").HasComment("Nonnegative experience placement component; nonzero only for Skill.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Level).HasColumnType("int").HasComment("Positive level derived by the approved curve; zero XP starts at level 1.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ProjectionVersion).HasColumnType("bigint").HasComment("Account projection generation to which this balance belongs.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => row.CategoryId, "IX_XpBalance_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.AttributeId, "IX_XpBalance_FK_Attribute").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.SystemSkillId, "IX_XpBalance_FK_Skill_System").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_XpBalance_FK_CustomSkill").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpBalance_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpBalance_Category");
        entity.HasOne<AttributeEntity>().WithMany().HasForeignKey(row => row.AttributeId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpBalance_Attribute");
        entity.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpBalance_Skill_System");
        entity.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpBalance_CustomSkill");
    }
}
