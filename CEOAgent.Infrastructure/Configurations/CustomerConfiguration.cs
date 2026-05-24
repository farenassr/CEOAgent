using CEOAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ExternalCustomerId).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(200);
        builder.HasIndex(entity => new { entity.CompanyChannelId, entity.ExternalCustomerId }).IsUnique();
        builder.HasOne(entity => entity.CompanyChannel)
            .WithMany(entity => entity.Customers)
            .HasForeignKey(entity => entity.CompanyChannelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
