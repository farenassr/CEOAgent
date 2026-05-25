using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class CompanyToolConfiguration : IEntityTypeConfiguration<CompanyTool>
{
    public void Configure(EntityTypeBuilder<CompanyTool> builder)
    {
        builder.ToTable("company_tool");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ToolKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(entity => new { entity.CompanyId, entity.ToolKey }).IsUnique();
        builder.ComplexProperty(entity => entity.Configuration, configuration =>
        {
            configuration.ToJson("configuration_json");
            configuration.Property(entity => entity.ToolKey).HasJsonPropertyName("toolKey");

            var checkAvailability = configuration.ComplexProperty(entity => entity.CheckAvailability);
            checkAvailability.HasJsonPropertyName("check_availability");
            checkAvailability.Property(entity => entity.MaxPartySize).HasJsonPropertyName("maxPartySize");
            checkAvailability.Property(entity => entity.MinPartySize).HasJsonPropertyName("minPartySize");
            checkAvailability.Property(entity => entity.SlotMinutes).HasJsonPropertyName("slotMinutes");
            checkAvailability.Property(entity => entity.AdvanceBookingDays).HasJsonPropertyName("advanceBookingDays");

            var requestHumanHandoff = configuration.ComplexProperty(entity => entity.RequestHumanHandoff);
            requestHumanHandoff.HasJsonPropertyName("request_human_handoff");
            requestHumanHandoff.Property(entity => entity.EscalationChannel).HasJsonPropertyName("escalationChannel");
            requestHumanHandoff.PrimitiveCollection(entity => entity.NotifyUsers).HasJsonPropertyName("notifyUsers");
            requestHumanHandoff.Property(entity => entity.TimeoutMinutes).HasJsonPropertyName("timeoutMinutes");

            var googleCalendar = configuration.ComplexProperty(entity => entity.GoogleCalendar);
            googleCalendar.HasJsonPropertyName("google_calendar");
            googleCalendar.Property(entity => entity.CalendarId).HasJsonPropertyName("calendarId");
            googleCalendar.Property(entity => entity.TimeZoneId).HasJsonPropertyName("timeZoneId");
            googleCalendar.Property(entity => entity.BufferMinutes).HasJsonPropertyName("bufferMinutes");
        });
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
