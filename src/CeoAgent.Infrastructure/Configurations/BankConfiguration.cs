using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("bank");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CountryCode).HasMaxLength(2).IsRequired();
        builder.HasIndex(entity => new { entity.CountryCode, entity.Name }).IsUnique();
        builder.HasIndex(entity => entity.IsActive);
    }
}
