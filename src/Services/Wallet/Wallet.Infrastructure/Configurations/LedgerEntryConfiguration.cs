using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallet.Domain;

namespace Wallet.Infrastructure.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.Reference).IsRequired();

        // Unique so a retried debit/credit (same key) can never produce a second
        // ledger entry - the idempotency check itself is an Application-layer
        // concern (Day 24), but the constraint that makes it actually safe lives here.
        builder.Property(e => e.IdempotencyKey).IsRequired();
        builder.HasIndex(e => e.IdempotencyKey).IsUnique();

        builder.HasIndex(e => e.AccountId);

        // No navigation property on Account by design (docs/Coding-Standards.md -
        // keep Domain entities plain); the FK relationship is configured here instead.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
