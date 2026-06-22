using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("message");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.MessageText).HasColumnName("message_text");
        builder.Property(entity => entity.ProviderMessageId).HasMaxLength(200);
        builder.ComplexProperty(entity => entity.Payload, payload =>
        {
            payload.ToJson("payload_json");
            payload.Property(entity => entity.ProviderType).HasJsonPropertyName("providerType");
            payload.Property(entity => entity.ProviderMessageId).HasJsonPropertyName("providerMessageId");
            payload.Property(entity => entity.ProviderMediaId).HasJsonPropertyName("providerMediaId");
            payload.Property(entity => entity.MimeType).HasJsonPropertyName("mimeType");
            payload.Property(entity => entity.Sha256).HasJsonPropertyName("sha256");
            payload.Property(entity => entity.BlobContainer).HasJsonPropertyName("blobContainer");
            payload.Property(entity => entity.BlobName).HasJsonPropertyName("blobName");
            payload.Property(entity => entity.BlobUri).HasJsonPropertyName("blobUri");
        });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.ProviderMessageId }).IsUnique()
            .HasFilter("provider_message_id IS NOT NULL");
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.ConversationId, entity.OccurredAt, entity.Id })
            .IsDescending(false, false, true, true);
        builder.HasOne(entity => entity.Conversation)
            .WithMany(entity => entity.Messages)
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
