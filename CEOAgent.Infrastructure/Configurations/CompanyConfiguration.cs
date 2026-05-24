using CEOAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CEOAgent.Infrastructure.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.TimeZoneId).HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.WorkingHours).HasJsonbConversion("working_hours_json");
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
