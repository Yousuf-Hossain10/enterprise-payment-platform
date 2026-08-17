using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain;

namespace Payment.Infrastructure.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment.Domain.Payment>
{
    public void Configure(EntityTypeBuilder<Payment.Domain.Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AccountId).IsRequired();
        builder.HasIndex(p => p.AccountId);

        builder.Property(p => p.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.Reference).IsRequired();

        builder.Property(p => p.IdempotencyKey).IsRequired();
        builder.HasIndex(p => p.IdempotencyKey).IsUnique();

        // Stored as text rather than the underlying int, so the database is
        // readable/queryable without a lookup table - readability wins over the
        // few bytes saved, since this table is small relative to LedgerEntries.
        builder.Property(p => p.Status).IsRequired().HasConversion<string>();
    }
}
