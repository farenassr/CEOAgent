using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class OutgoingMessageOutboxConfiguration : IEntityTypeConfiguration<OutgoingMessageOutbox>
{
    public void Configure(EntityTypeBuilder<OutgoingMessageOutbox> builder)
    {
        builder.ToTable("outgoing_message_outbox");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.ProviderMessageId).HasMaxLength(200);
        builder.Property(entity => entity.ClaimedBy).HasMaxLength(120);
        builder.Property(entity => entity.CorrelationId).HasMaxLength(120);
        builder.Property(entity => entity.LastError).HasMaxLength(500);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IdempotencyKey }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.NextAttemptAt, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.Conversation)
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Message)
            .WithMany()
            .HasForeignKey(entity => entity.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
