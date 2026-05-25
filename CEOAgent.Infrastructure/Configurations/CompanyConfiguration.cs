using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.TimeZoneId).HasMaxLength(120).IsRequired();
        builder.ComplexProperty(entity => entity.WorkingHours, workingHours =>
        {
            workingHours.ToJson("working_hours_json");

            var schedule = workingHours.ComplexProperty(entity => entity.Schedule);
            schedule.HasJsonPropertyName("schedule");
            schedule.ComplexCollection(entity => entity.Monday).HasJsonPropertyName("monday");
            schedule.ComplexCollection(entity => entity.Tuesday).HasJsonPropertyName("tuesday");
            schedule.ComplexCollection(entity => entity.Wednesday).HasJsonPropertyName("wednesday");
            schedule.ComplexCollection(entity => entity.Thursday).HasJsonPropertyName("thursday");
            schedule.ComplexCollection(entity => entity.Friday).HasJsonPropertyName("friday");
            schedule.ComplexCollection(entity => entity.Saturday).HasJsonPropertyName("saturday");
            schedule.ComplexCollection(entity => entity.Sunday).HasJsonPropertyName("sunday");

            var holidays = workingHours.ComplexCollection(entity => entity.Holidays);
            holidays.HasJsonPropertyName("holidays");
            holidays.Property(entity => entity.Date).HasJsonPropertyName("date");
            holidays.Property(entity => entity.IsClosed).HasJsonPropertyName("isClosed");
            holidays.ComplexCollection(entity => entity.TimeSlots).HasJsonPropertyName("timeSlots");
            holidays.Property(entity => entity.Reason).HasJsonPropertyName("reason");
        });
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
