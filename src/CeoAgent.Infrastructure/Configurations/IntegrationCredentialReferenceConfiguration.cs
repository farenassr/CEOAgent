using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CeoAgent.Infrastructure.Configurations;

public sealed class IntegrationCredentialReferenceConfiguration : IEntityTypeConfiguration<IntegrationCredentialReference>
{
    public void Configure(EntityTypeBuilder<IntegrationCredentialReference> builder)
    {
        builder.ToTable("integration_credential_reference");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider)
            .HasConversion(
                provider => ToProviderName(provider),
                provider => ToIntegrationProvider(provider))
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(entity => entity.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Reference).HasMaxLength(300).IsRequired();
        builder.HasIndex(entity => new { entity.CompanyId, entity.Provider, entity.Purpose }).IsUnique();
        builder.HasIndex(entity => new { entity.CompanyId, entity.CreatedAt }).IsDescending(false, true);
        builder.ComplexProperty(entity => entity.Metadata, metadata =>
        {
            metadata.ToJson("metadata_json");
            metadata.Property(entity => entity.Provider).HasJsonPropertyName("provider");

            var googleCalendar = metadata.ComplexProperty(entity => entity.GoogleCalendar);
            googleCalendar.HasJsonPropertyName("google_calendar");
            googleCalendar.Property(entity => entity.CalendarId).HasJsonPropertyName("calendarId");
            googleCalendar.Property(entity => entity.Scope).HasJsonPropertyName("scope");
            googleCalendar.Property(entity => entity.ExpiresAt).HasJsonPropertyName("expiresAt");

            var whatsAppCloud = metadata.ComplexProperty(entity => entity.WhatsAppCloud);
            whatsAppCloud.HasJsonPropertyName("whatsapp_cloud");
            whatsAppCloud.Property(entity => entity.AppId).HasJsonPropertyName("appId");
            whatsAppCloud.Property(entity => entity.TokenVersion).HasJsonPropertyName("tokenVersion");

        });
        builder.HasOne(entity => entity.Company)
            .WithMany(entity => entity.IntegrationCredentials)
            .HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static string ToProviderName(IntegrationProvider provider)
    {
        return provider switch
        {
            IntegrationProvider.WhatsAppCloud => "whatsapp_cloud",
            IntegrationProvider.GoogleCalendar => "google_calendar",
            _ => throw new InvalidOperationException($"Integration provider '{provider}' is not supported."),
        };
    }

    private static IntegrationProvider ToIntegrationProvider(string provider)
    {
        return provider switch
        {
            "whatsapp_cloud" => IntegrationProvider.WhatsAppCloud,
            "google_calendar" => IntegrationProvider.GoogleCalendar,
            _ => throw new InvalidOperationException($"Integration provider '{provider}' is not supported."),
        };
    }
}
