using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversation");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.LlmProvider).HasConversion<string>().HasMaxLength(32);
        builder.Property(entity => entity.ModelName).HasMaxLength(120);
        builder.Property(entity => entity.ProviderConversationId).HasMaxLength(240);
        builder.Property(entity => entity.ProviderLastResponseId).HasMaxLength(240);
        builder.Property(entity => entity.AgentSessionJson);
        builder.Property(entity => entity.AgentSessionResetReason).HasMaxLength(64);
        builder.Property(entity => entity.AgentSessionTurnCount).HasDefaultValue(0).IsRequired();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CustomerId, entity.CompanyChannelId }).IsUnique()
            .HasFilter("status = 'Open'");
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.Customer)
            .WithMany(entity => entity.Conversations)
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CompanyChannel)
            .WithMany(entity => entity.Conversations)
            .HasForeignKey(entity => entity.CompanyChannelId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.AgentProfile)
            .WithMany(entity => entity.Conversations)
            .HasForeignKey(entity => entity.AgentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
