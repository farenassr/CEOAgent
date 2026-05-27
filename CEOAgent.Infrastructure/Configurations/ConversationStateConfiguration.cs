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
        builder.ComplexProperty(entity => entity.Snapshot, snapshot =>
        {
            snapshot.IsRequired();
            snapshot.ToJson("state_json");
            snapshot.Property(entity => entity.CurrentIntent).HasJsonPropertyName("currentIntent");
            snapshot.Property(entity => entity.PendingAction).HasJsonPropertyName("pendingAction");
            var slots = snapshot.ComplexCollection(entity => entity.Slots);
            slots.HasJsonPropertyName("slots");
            slots.Property(entity => entity.Name).HasJsonPropertyName("name");
            slots.Property(entity => entity.TextValue).HasJsonPropertyName("textValue");
            slots.Property(entity => entity.NumberValue).HasJsonPropertyName("numberValue");
            slots.Property(entity => entity.BooleanValue).HasJsonPropertyName("booleanValue");
            slots.Property(entity => entity.DateValue).HasJsonPropertyName("dateValue");
            slots.Property(entity => entity.TimeValue).HasJsonPropertyName("timeValue");
            snapshot.PrimitiveCollection(entity => entity.ConversationFlags).HasJsonPropertyName("conversationFlags");
            snapshot.Property(entity => entity.TurnCount).HasJsonPropertyName("turnCount");
        });
        builder.HasOne(entity => entity.Conversation)
            .WithOne(entity => entity.State)
            .HasForeignKey<ConversationState>(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
