using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ChannelType).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.ExternalCustomerId).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200);
        builder.HasIndex(entity => new { entity.CompanyId, entity.ChannelType, entity.ExternalCustomerId }).IsUnique();
    }
}
