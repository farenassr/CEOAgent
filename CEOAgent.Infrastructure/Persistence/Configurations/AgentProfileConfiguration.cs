using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

public sealed class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("agent_profile");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ModelName).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Language).HasMaxLength(16).IsRequired();
        builder.HasIndex(entity => entity.CompanyId).IsUnique();
        builder.HasOne(entity => entity.Company)
            .WithOne(entity => entity.AgentProfile)
            .HasForeignKey<AgentProfile>(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
