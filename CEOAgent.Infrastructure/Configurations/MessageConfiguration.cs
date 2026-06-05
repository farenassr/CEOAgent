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

            var audio = payload.ComplexProperty(entity => entity.Audio);
            audio.HasJsonPropertyName("audio");
            audio.Property(entity => entity.BlobUri).HasJsonPropertyName("blobUri");
            audio.Property(entity => entity.ContentType).HasJsonPropertyName("contentType");
            audio.Property(entity => entity.SizeBytes).HasJsonPropertyName("sizeBytes");
            audio.Property(entity => entity.ProviderMediaId).HasJsonPropertyName("providerMediaId");
            audio.Property(entity => entity.Language).HasJsonPropertyName("language");
            audio.Property(entity => entity.DurationMs).HasJsonPropertyName("durationMs");
            audio.Property(entity => entity.SttStatus).HasConversion<string>().HasJsonPropertyName("sttStatus");
            audio.Property(entity => entity.TtsStatus).HasConversion<string>().HasJsonPropertyName("ttsStatus");
        });
        builder.HasIndex(entity => new { entity.CompanyId, entity.ProviderMessageId }).IsUnique()
            .HasFilter("provider_message_id IS NOT NULL");
        builder.HasIndex(entity => new { entity.CompanyId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(entity => new { entity.CompanyId, entity.ConversationId, entity.OccurredAt, entity.Id })
            .IsDescending(false, false, true, true);
        builder.HasOne(entity => entity.Conversation)
            .WithMany(entity => entity.Messages)
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
