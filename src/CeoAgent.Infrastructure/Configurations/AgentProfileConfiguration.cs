using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("agent_profile");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ModelName).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Language).HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.MaxOutputTokenCount).HasDefaultValue(1024).IsRequired();
        builder.Property(entity => entity.MaxEstimatedCostUsdPerJob).HasDefaultValue(0.05d).IsRequired();
        builder.HasIndex(entity => entity.OrganizationId).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.Company)
            .WithOne(entity => entity.AgentProfile)
            .HasForeignKey<AgentProfile>(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
