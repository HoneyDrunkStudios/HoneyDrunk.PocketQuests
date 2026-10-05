using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Categories;

namespace PocketQuests.Data.Configurations.Accounts;

/// <summary>Maps AccountInterest fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class AccountInterestMapping : IEntityTypeConfiguration<AccountInterestEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountInterestEntity> entity)
    {
        entity.ToTable("AccountInterest", "pocketquests", table =>
        {
            table.HasComment("One row is one current category selected as an account interest. Classification: Restricted. History: event; Current association only; remove when deselected and at final erasure. No historical interest tracking.");
            table.HasCheckConstraint("CK_AccountInterest_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
            table.HasCheckConstraint("CK_AccountInterest_Position", "[Position]>=0 AND [Position]<10");
        });
        entity.HasKey(row => new { row.AccountId, row.CategoryId }).HasName("PK_AccountInterest").IsClustered();
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CategoryId).HasComment("Selected category; never enables recurrence implicitly.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.Position).HasComment("Current selected category array position, zero-based and at most nine.").HasDefaultValueSql("0");
        entity.HasIndex(row => row.CategoryId, "IX_AccountInterest_FK_Category");
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountInterest_Account");
        entity.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccountInterest_Category");
    }
}
