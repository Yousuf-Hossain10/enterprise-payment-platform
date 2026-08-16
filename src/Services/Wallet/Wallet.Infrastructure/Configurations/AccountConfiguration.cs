using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Domain;

namespace Wallet.Infrastructure.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OwnerId).IsRequired();
        builder.HasIndex(a => a.OwnerId);

        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);

        // Postgres' native xmin system column as the optimistic concurrency token -
        // it's bumped by the database on every row update with no app-side counter
        // to maintain, unlike SQL Server's rowversion.
        builder.Property(a => a.RowVersion)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
