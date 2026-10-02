using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookExchange.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(OutboxMessage.TypeMaxLength).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

        // SPEC §10.1 index, plus a partial index that matches the processor's polling query exactly.
        builder.HasIndex(m => new { m.ProcessedAt, m.CreatedAt });
        builder.HasIndex(m => m.NextAttemptAt)
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
    }
}
