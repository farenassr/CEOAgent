using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class MessageDispatchConfiguration : IEntityTypeConfiguration<MessageDispatch>
{
    public void Configure(EntityTypeBuilder<MessageDispatch> builder)
    {
        builder.ToTable("message_dispatch");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Operation).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.ClaimedBy).HasMaxLength(120);
        builder.Property(entity => entity.ProviderMessageId).HasMaxLength(200);
        builder.Property(entity => entity.LastError).HasMaxLength(500);
        builder.Property(entity => entity.CorrelationId).HasMaxLength(120);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.MessageId, entity.Operation }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Operation, entity.IdempotencyKey }).IsUnique();
        builder.HasIndex(entity => new { entity.Operation, entity.Status, entity.NextAttemptAt, entity.CreatedAt });
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
