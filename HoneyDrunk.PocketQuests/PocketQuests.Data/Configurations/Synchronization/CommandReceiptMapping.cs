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
        entity.Property(row => row.Id).HasComment("Stable client operation UUID; unchanged across retries.").ValueGeneratedNever().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AccountId).HasComment("Pocket Quests account that exclusively owns this row; supplied by trusted server context.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CommandType).HasComment("Typed command discriminator under the stored API contract version.").UseCollation("Latin1_General_100_BIN2").HasMaxLength(80).IsUnicode(false).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.ApiVersion).HasComment("Positive version of the accepted wire command contract.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.PayloadDigest).HasColumnType("binary(32)").HasComment("SHA-256 of the versioned canonical command representation, excluding authentication secrets.").HasMaxLength(32).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.DigestVersion).HasComment("Positive canonicalization/digest schema version; never hash incidental serializer output.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OutcomeVersion).HasComment("Positive schema version of the compact original command outcome.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.OutcomeJson).HasComment("Immutable compact original result and feedback, never the whole account or raw provider credentials. Limited to 65536 UTF-16 bytes.").UseCollation("Latin1_General_100_BIN2").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.AppliedMutationVersion).HasComment("Committed account mutation represented by this outcome; unchanged on retry.").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.Property(row => row.CreatedAt).HasComment("Server UTC insertion time; not the effective time of a delayed offline action.").HasPrecision(7).HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')").Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        entity.HasOne<AccountEntity>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CommandReceipt_Account");
    }
}
