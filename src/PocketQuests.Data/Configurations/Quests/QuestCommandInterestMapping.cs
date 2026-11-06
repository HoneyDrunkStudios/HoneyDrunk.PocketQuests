using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Categories;
using PocketQuests.Data.Entities.Quests;

namespace PocketQuests.Data.Configurations.Quests;

/// <summary>Maps QuestCommandInterest fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class QuestCommandInterestMapping : IEntityTypeConfiguration<QuestCommandInterestEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestCommandInterestEntity> builder)
    {
        builder.ToTable("QuestCommandInterest", "pocketquests", table =>
        {
            table.HasComment("One row is one ordered category selected by a successful interests command, retained only to reconstruct its historical response. Classification: Restricted. History: immutable. Erase with its account; no age cutoff on replay history.");
            table.HasCheckConstraint("CK_QuestCommandInterest_Position", "[Position]>=0 AND [Position]<10");
            table.HasCheckConstraint("CK_QuestCommandInterest_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        builder.HasKey(row => new { row.AccountId, row.QuestCommandHistoryId, row.CategoryId }).HasName("PK_QuestCommandInterest").IsClustered();
        builder.HasIndex(row => new { row.AccountId, row.QuestCommandHistoryId, row.Position }, "UQ_QuestCommandInterest_Position").IsUnique().HasFilter(null);
        builder.Property(row => row.AccountId).HasComment("Owning account, shared with the parent history row.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.QuestCommandHistoryId).HasComment("Immutable interests transition owning this selected category.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CategoryId).HasComment("Selected public category code.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(40).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.Position).HasComment("Original distinct selection order, matching the existing profile array.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(row => row.CreatedAt).HasComment("UTC server insertion instant.").HasPrecision(7).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.HasIndex(row => row.CategoryId, "IX_QuestCommandInterest_CategoryId");
        builder.HasOne<QuestCommandHistoryEntity>().WithMany().HasForeignKey(row => new { row.AccountId, row.QuestCommandHistoryId }).HasPrincipalKey(row => new { row.AccountId, row.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandInterest_History");
        builder.HasOne<CategoryEntity>().WithMany().HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestCommandInterest_Category");
    }
}
