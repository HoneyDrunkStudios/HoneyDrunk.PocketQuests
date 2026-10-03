using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PocketQuests.Data.Entities;

namespace PocketQuests.Data.Configurations;

/// <summary>Fluent mapping for the Accounts table.</summary>
public sealed class AccountEntityConfiguration : IEntityTypeConfiguration<AccountEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccountEntity> builder)
    {
        builder.ToTable("Accounts", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdentityKey).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => x.IdentityKey).IsUnique();
        builder.Property(x => x.Zone).HasMaxLength(100);
    }
}
