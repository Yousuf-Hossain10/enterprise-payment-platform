using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Payment.Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.Payload).IsRequired();

        // OutboxDispatcherBackgroundService polls for unprocessed messages on this
        // filter - an index keeps that query cheap as the table grows.
        builder.HasIndex(m => m.ProcessedAtUtc);
    }
}
