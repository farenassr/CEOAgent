using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class IncomingMessageOutboxConfiguration : IEntityTypeConfiguration<IncomingMessageOutbox>
{
    public void Configure(EntityTypeBuilder<IncomingMessageOutbox> builder)
    {
        builder.ToTable("incoming_message_outbox");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CorrelationId).HasMaxLength(120);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.FailureReason).HasMaxLength(240);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.MessageId }).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.CreatedAt }).IsDescending(false, false);
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
