using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class CompanyChannelConfiguration : IEntityTypeConfiguration<CompanyChannel>
{
    public void Configure(EntityTypeBuilder<CompanyChannel> builder)
    {
        builder.ToTable("company_channel");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.ProviderChannelId).HasMaxLength(160).IsRequired();
        builder.ComplexProperty(entity => entity.Metadata, metadata =>
        {
            metadata.ToJson("metadata_json");
            metadata.ComplexProperty(entity => entity.WhatsAppCloud).HasJsonPropertyName("whatsapp_cloud");
            metadata.ComplexProperty(entity => entity.Instagram).HasJsonPropertyName("instagram");
            metadata.ComplexProperty(entity => entity.Telegram).HasJsonPropertyName("telegram");
        });
        builder.HasIndex(entity => entity.Provider);
        builder.HasIndex(entity => new { entity.Provider, entity.ProviderChannelId }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt }).IsDescending(false, true);
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.Channels)
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CredentialReference)
            .WithMany(entity => entity.CompanyChannels)
            .HasForeignKey(entity => entity.CredentialReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
