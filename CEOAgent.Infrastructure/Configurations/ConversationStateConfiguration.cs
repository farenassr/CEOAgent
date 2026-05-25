using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class ConversationStateConfiguration : IEntityTypeConfiguration<ConversationState>
{
    public void Configure(EntityTypeBuilder<ConversationState> builder)
    {
        builder.ToTable("conversation_state");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.ConversationId).IsUnique();
        builder.Property(entity => entity.Snapshot).HasJsonbConversion("state_json").IsRequired();
        builder.HasOne(entity => entity.Conversation)
            .WithOne(entity => entity.State)
            .HasForeignKey<ConversationState>(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
