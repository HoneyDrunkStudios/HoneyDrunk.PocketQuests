using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities.Accounts;
using PocketQuests.Data.Entities.Synchronization;

namespace PocketQuests.Data.Configurations.Synchronization;

/// <summary>Maps CommandReceipt fields, metadata and relational constraints to the DACPAC-owned table.</summary>
public sealed class CommandReceiptMapping : IEntityTypeConfiguration<CommandReceiptEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CommandReceiptEntity> entity)
    {
        entity.Property(row => row.PayloadDigest).Metadata.SetValueComparer(new ValueComparer<byte[]>(
            (first, second) => first != null && second != null ? first.SequenceEqual(second) : first == second,
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToArray()));
        entity.ToTable("CommandReceipt", "pocketquests", table =>
        {
            table.HasComment("One row is one immutable original outcome of a committed account command. Classification: Restricted. History: plumbing; Retain compact outcome and deduplication digest while the account exists. Delete at final account erasure; no replay TTL.");
            table.HasCheckConstraint("CK_CommandReceipt_ApiVersion", "[ApiVersion] >= 1");
            table.HasCheckConstraint("CK_CommandReceipt_DigestVersion", "[DigestVersion] >= 1");
            table.HasCheckConstraint("CK_CommandReceipt_OutcomeVersion", "[OutcomeVersion] >= 1");
            table.HasCheckConstraint("CK_CommandReceipt_AppliedMutationVersion", "[AppliedMutationVersion] >= 0");
            table.HasCheckConstraint("CK_CommandReceipt_OutcomeDocument", "ISJSON([OutcomeJson])=1 AND DATALENGTH([OutcomeJson])<=65536");
            table.HasCheckConstraint("CK_CommandReceipt_CreatedAtUtc", "DATEPART(TZOFFSET,[CreatedAt])=0");
        });
        entity.HasKey(row => row.Id).HasName("PK_CommandReceipt").IsClustered();
        entity.HasAlternateKey(row => new { row.AccountId, row.Id }).HasName("UQ_CommandReceipt_AccountId_Id");
        entity.Property(row => row.Id).HasColumnType("uniqueidentifier").HasComment("Stable client operation UUID; unchanged across retries.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasColumnType("uniqueidentifier").HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandType).HasColumnType("varchar(80)").HasComment("Typed command discriminator under the stored API contract version.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(false).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ApiVersion).HasColumnType("smallint").HasComment("Positive version of the accepted wire command contract.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.PayloadDigest).HasColumnType("binary(32)").HasComment("SHA-256 of the versioned canonical command representation, excluding authentication secrets.").HasMaxLength(32).IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DigestVersion).HasColumnType("smallint").HasComment("Positive canonicalization/digest schema version; never hash incidental serializer output.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OutcomeVersion).HasColumnType("smallint").HasComment("Positive schema version of the compact original command outcome.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OutcomeJson).HasColumnType("nvarchar(max)").HasComment("Immutable compact original result and feedback, never the whole account or raw provider credentials. Limited to 65536 UTF-16 bytes.").UseCollation("Latin1_General_100_BIN2").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AppliedMutationVersion).HasColumnType("bigint").HasComment("Committed account mutation represented by this outcome; unchanged on retry.").IsRequired(true).ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasColumnType("datetimeoffset(7)").HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).IsRequired(true).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).HasPrincipalKey(row => row.Id).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CommandReceipt_Account");
    }
}
