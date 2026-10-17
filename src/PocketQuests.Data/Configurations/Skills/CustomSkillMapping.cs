using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Skills;

namespace PocketQuests.Data.Configurations.Skills;

/// <summary>Maps CustomSkill fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class CustomSkillMapping : IEntityTypeConfiguration<CustomSkillEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomSkillEntity> entity)
    {
        entity.ToTable("CustomSkill", "pocketquests", table =>
        {
            table.HasComment("One row is one account-owned custom skill, including archived skills. Classification: Restricted. History: mutable; Retain while the account exists, including its recovery interval; erase with the account. No arbitrary age-based pruning of replay source facts.");
            table.HasCheckConstraint("CK_CustomSkill_Revision", "[Revision] >= 1");
            table.HasCheckConstraint("CK_CustomSkill_NameNormalizationVersion", "[NameNormalizationVersion] >= 1");
            table.HasCheckConstraint("CK_CustomSkill_Name", "LEN([Name]) BETWEEN 1 AND 80 AND DATALENGTH([Name])=DATALENGTH(TRIM([Name])) AND LEN([NormalizedName])>0");
            table.HasCheckConstraint("CK_CustomSkill_ArchivedAtUtc", "[ArchivedAt] IS NULL OR DATEPART(TZOFFSET,[ArchivedAt])=0");
            table.HasCheckConstraint("CK_CustomSkill_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_CustomSkill_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_CustomSkill_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
            table.HasCheckConstraint("CK_CustomSkill_CreationOrdinal", "[CreationOrdinal]>=0");
            table.HasCheckConstraint("CK_CustomSkill_ClientKey", "[ClientKey] IS NULL OR (DATALENGTH([ClientKey])=36 AND TRY_CAST([ClientKey] AS uniqueidentifier) IS NOT NULL AND TRY_CAST([ClientKey] AS uniqueidentifier)=[Id])");
        });
        entity.HasKey(row => row.Id).HasName("PK_CustomSkill").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_CustomSkill_AccountId_Id");
        entity.HasIndex(row => new { row.AccountId, row.NormalizedName }, "UQ_CustomSkill_AccountId_NormalizedName").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasComment("Application-generated stable row UUID; never reused.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Name).HasComment("Trimmed custom skill display name, 1 to 80 characters.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(true);
        entity.Property(row => row.NormalizedName).HasComment("Versioned OrdinalIgnoreCase-equivalent name key; binary comparison. Archived names remain reserved.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(true);
        entity.Property(row => row.NameNormalizationVersion).HasComment("Positive version of the shared product name-normalization algorithm.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Revision).HasComment("Positive optimistic business revision, distinct from SQL RowVersion.");
        entity.Property(row => row.ArchivedAt).HasComment("Instant the skill was archived; not a deletion marker. UTC instant. Null means this event has not happened.").HasPrecision(7);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ModifiedAt).HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.RowVersion).HasColumnType("rowversion").HasComment("SQL-generated opaque concurrency token; compare as bytes, not as a clock.").IsRowVersion();
        entity.Property(row => row.CreationOrdinal).HasComment("Stable one-based position in the existing API array, set once at creation; zero is reserved for public catalog adoption or the retained v1 adapter.").HasDefaultValueSql("0").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ClientKey).HasComment("Original accepted UUID spelling for API compatibility; the typed Id remains the ownership key. Null is reserved for public catalog adoption or the retained v1 adapter.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(36).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CustomSkill_Account");
    }
}
