using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Attributes;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Progress;
using PocketQuests.Data.Entities.Quests;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Mappings.Progress;

/// <summary>Maps XpLedgerEntry fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class XpLedgerEntryMapping : IEntityTypeConfiguration<XpLedgerEntryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<XpLedgerEntryEntity> entity)
    {
        entity.ToTable("XpLedgerEntry", "pocketquests", table =>
        {
            table.HasComment("One row is one currently effective derived contribution from an authoritative occurrence event. Classification: Restricted. History: projection; Retain only the current derived ledger; replace affected suffix transactionally. Source events remain authoritative. Erase with account.");
            table.HasCheckConstraint("CK_XpLedgerEntry_TrackTarget", "([TrackCode]='Overall' AND [CategoryId] IS NULL AND [AttributeId] IS NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Category' AND [CategoryId] IS NOT NULL AND [AttributeId] IS NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Attribute' AND [CategoryId] IS NULL AND [AttributeId] IS NOT NULL AND [SystemSkillId] IS NULL AND [CustomSkillId] IS NULL) OR ([TrackCode]='Skill' AND [CategoryId] IS NULL AND [AttributeId] IS NULL AND (([SystemSkillId] IS NOT NULL AND [CustomSkillId] IS NULL) OR ([SystemSkillId] IS NULL AND [CustomSkillId] IS NOT NULL)))");
            table.HasCheckConstraint("CK_XpLedgerEntry_ContributionCode", "[ContributionCode] IN ('Base','StreakBonus','Penalty')");
            table.HasCheckConstraint("CK_XpLedgerEntry_ProjectionVersion", "[ProjectionVersion] >= 0");
            table.HasCheckConstraint("CK_XpLedgerEntry_SignAndPool", "([ContributionCode]='Penalty' AND [Amount]<=0 AND [TrackCode]='Category') OR ([ContributionCode] IN ('Base','StreakBonus') AND [Amount]>=0 AND ([ContributionCode]<>'StreakBonus' OR [TrackCode]='Category'))");
            table.HasCheckConstraint("CK_XpLedgerEntry_EffectiveAtUtc", "DATEPART(TZOFFSET,[EffectiveAt])=0");
            table.HasCheckConstraint("CK_XpLedgerEntry_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_XpLedgerEntry_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_XpLedgerEntry_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_XpLedgerEntry").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_XpLedgerEntry_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.QuestOccurrenceEventId, row.ContributionCode, row.TrackCode, row.CategoryId, row.AttributeId, row.SystemSkillId, row.CustomSkillId }, "UQ_XpLedgerEntry_SourceContributionTarget").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Stable derived-entry UUID from source event, contribution kind and target identity.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.QuestOccurrenceEventId).HasColumnType("uniqueidentifier").HasComment("Source event whose contribution is represented.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ContributionCode).HasColumnType("varchar(12)").HasComment("Base, StreakBonus or Penalty; placement seeds are separate.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.TrackCode).HasColumnType("varchar(12)").HasComment("Progression pool: Overall, Category, Attribute or Skill.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(12).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Category target; null unless TrackCode is Category.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.AttributeId).HasColumnType("varchar(40)").HasComment("Attribute target; null unless TrackCode is Attribute.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.SystemSkillId).HasColumnType("varchar(40)").HasComment("System skill target; null for other pools or a custom skill.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.CustomSkillId).HasColumnType("uniqueidentifier").HasComment("Account-owned custom skill target; null for other pools or a system skill.").IsRequired(false).ValueGeneratedNever();
        entity.Property(row => row.EffectiveAt).HasColumnType("datetimeoffset(7)").HasComment("Chronological application instant of the contribution. UTC instant.").HasPrecision(7).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.Amount).HasColumnType("bigint").HasComment("Signed exact XP change; negative values are permitted for penalties only.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ProjectionVersion).HasColumnType("bigint").HasComment("Account projection generation represented by this current derived row.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.RulesetVersion).HasColumnType("varchar(32)").HasComment("Ruleset used when deriving this contribution.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(32).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => new { row.AccountId, row.EffectiveAt, row.Id }, "IX_XpLedgerEntry_Timeline").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.CategoryId, "IX_XpLedgerEntry_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.AttributeId, "IX_XpLedgerEntry_FK_Attribute").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => row.SystemSkillId, "IX_XpLedgerEntry_FK_Skill_System").IsUnique(false).HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CustomSkillId }, "IX_XpLedgerEntry_FK_CustomSkill").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_Category");
        entity.HasOne<AttributeEntity>().WithMany().HasForeignKey(row => row.AttributeId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_Attribute");
        entity.HasOne<SkillEntity>().WithMany().HasForeignKey(row => row.SystemSkillId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_Skill_System");
        entity.HasOne<CustomSkillEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.CustomSkillId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_CustomSkill");
        entity.HasOne<QuestOccurrenceEventEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestOccurrenceEventId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_XpLedgerEntry_QuestOccurrenceEvent");
    }
}
