using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class IntegrationCredentialReferenceConfiguration : IEntityTypeConfiguration<IntegrationCredentialReference>
{
    public void Configure(EntityTypeBuilder<IntegrationCredentialReference> builder)
    {
        builder.ToTable("integration_credential_reference");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Reference).HasMaxLength(300).IsRequired();
        builder.HasIndex(entity => new { entity.CompanyId, entity.Provider, entity.Purpose }).IsUnique();
        builder.Property(entity => entity.Metadata).HasJsonbConversion("metadata_json");
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.IntegrationCredentials)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
