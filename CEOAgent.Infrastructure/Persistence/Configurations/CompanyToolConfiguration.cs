using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

public sealed class CompanyToolConfiguration : IEntityTypeConfiguration<CompanyTool>
{
    public void Configure(EntityTypeBuilder<CompanyTool> builder)
    {
        builder.ToTable("company_tool");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ToolKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(entity => new { entity.CompanyId, entity.ToolKey }).IsUnique();
        builder.Property(entity => entity.Configuration).HasJsonbConversion("configuration_json");
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.Tools)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CredentialReference)
            .WithMany(entity => entity.CompanyTools)
            .HasForeignKey(entity => entity.CredentialReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
