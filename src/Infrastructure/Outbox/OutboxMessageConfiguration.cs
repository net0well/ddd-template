using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public const int TypeMaxLength = 500;

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(TypeMaxLength).IsRequired();
        builder.Property(message => message.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.TraceParent).HasMaxLength(128);

        // Only deliverable messages are indexed: polling stays cheap however many processed rows or dead letters
        // the table keeps.
        builder.HasIndex(message => message.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL");

        // Used by the retention cleanup.
        builder.HasIndex(message => message.ProcessedOnUtc)
            .HasDatabaseName("ix_outbox_messages_processed")
            .HasFilter("processed_on_utc IS NOT NULL");

        // Used by the health check and by whoever inspects failures.
        builder.HasIndex(message => message.DeadLetteredOnUtc)
            .HasDatabaseName("ix_outbox_messages_dead_lettered")
            .HasFilter("dead_lettered_on_utc IS NOT NULL");
    }
}

internal sealed class OutboxMessageConsumerConfiguration : IEntityTypeConfiguration<OutboxMessageConsumer>
{
    public void Configure(EntityTypeBuilder<OutboxMessageConsumer> builder)
    {
        builder.ToTable("outbox_message_consumers");

        builder.HasKey(consumer => new { consumer.OutboxMessageId, consumer.Handler });
        builder.Property(consumer => consumer.Handler).HasMaxLength(OutboxMessageConfiguration.TypeMaxLength);

        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(consumer => consumer.OutboxMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
