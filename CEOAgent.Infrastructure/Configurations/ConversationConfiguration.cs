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
        builder.HasIndex(entity => new { entity.CompanyId, entity.CustomerId, entity.CompanyChannelId }).IsUnique()
            .HasFilter("status = 'Open'");
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
    }
}
