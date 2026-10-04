using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.Mappings.Categories;

/// <summary>Maps CategoryProgress fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class CategoryProgressMapping : IEntityTypeConfiguration<CategoryProgressEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CategoryProgressEntity> entity)
    {
        entity.ToTable("CategoryProgress", "pocketquests", table =>
        {
            table.HasComment("One row is one current account/category streak and explicit pause setting. Classification: Restricted. History: projection; Current projection plus explicit category switch; source pause intervals are retained. Erase with account.");
            table.HasCheckConstraint("CK_CategoryProgress_StreakDays", "[StreakDays] >= 0");
            table.HasCheckConstraint("CK_CategoryProgress_BonusRatePercent", "[BonusRatePercent] BETWEEN 0 AND 20");
            table.HasCheckConstraint("CK_CategoryProgress_ProjectionVersion", "[ProjectionVersion] >= 0");
            table.HasCheckConstraint("CK_CategoryProgress_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_CategoryProgress_ModifiedAtUtc", "DATEPART(TZOFFSET,[ModifiedAt])=0");
            table.HasCheckConstraint("CK_CategoryProgress_ModificationClock", "[ModifiedAt]>=[CreatedAt]");
        });
        entity.HasKey(row => row.Id).HasName("PK_CategoryProgress").IsClustered();
        entity.HasIndex(row => new { row.AccountId, row.Id }, "UQ_CategoryProgress_AccountId_Id").IsUnique().HasFilter(null);
        entity.HasIndex(row => new { row.AccountId, row.CategoryId }, "UQ_CategoryProgress_AccountId_CategoryId").IsUnique().HasFilter(null);
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Application-generated stable row UUID; never reused.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CategoryId).HasColumnType("varchar(40)").HasComment("Category whose streak is displayed.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.StreakDays).HasColumnType("int").HasComment("Nonnegative category streak length under pause/zone history.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.BonusRatePercent).HasColumnType("int").HasComment("Integer category streak bonus percentage, from 0 through the approved maximum 20 under PQ-MVP-013.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.HasQualifiedToday).HasColumnType("bit").HasComment("Whether the account qualified in this category on AsOfDate.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.IsExplicitlyPaused).HasColumnType("bit").HasComment("Current explicit category-pause switch; distinct from the account-wide switch.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.AsOfDate).HasColumnType("date").HasComment("Account-calendar date used for the current-day flag.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.TimeZoneId).HasColumnType("varchar(100)").HasComment("IANA timezone used for AsOfDate.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(100).IsUnicode(false).IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.ProjectionVersion).HasColumnType("bigint").HasComment("Account projection generation of this summary.").IsRequired(true).ValueGeneratedNever();
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.Property(row => row.ModifiedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC time of the most recent persisted change; writer must set on each update.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        entity.HasIndex(row => row.CategoryId, "IX_CategoryProgress_FK_Category").IsUnique(false).HasFilter(null);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CategoryProgress_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CategoryProgress_Category");
    }
}
