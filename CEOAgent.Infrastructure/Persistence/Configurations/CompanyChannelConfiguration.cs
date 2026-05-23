using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

public sealed class CompanyChannelConfiguration : IEntityTypeConfiguration<CompanyChannel>
{
    public void Configure(EntityTypeBuilder<CompanyChannel> builder)
    {
        builder.ToTable("company_channel");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.ProviderChannelId).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Metadata).HasJsonbConversion("metadata_json");
        builder.HasIndex(entity => new { entity.Provider, entity.ProviderChannelId }).IsUnique();
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.Channels)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CredentialReference)
            .WithMany(entity => entity.CompanyChannels)
            .HasForeignKey(entity => entity.CredentialReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
