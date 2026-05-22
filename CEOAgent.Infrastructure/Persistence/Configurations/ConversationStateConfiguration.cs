using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

public sealed class ConversationStateConfiguration : IEntityTypeConfiguration<ConversationState>
{
    public void Configure(EntityTypeBuilder<ConversationState> builder)
    {
        builder.ToTable("conversation_state");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.ConversationId).IsUnique();
        builder.Property(entity => entity.StateJson).HasColumnType("jsonb");
        builder.HasOne(entity => entity.Conversation)
            .WithOne(entity => entity.State)
            .HasForeignKey<ConversationState>(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
