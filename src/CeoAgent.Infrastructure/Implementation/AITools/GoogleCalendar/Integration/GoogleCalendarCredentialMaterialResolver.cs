using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;

public static class GoogleCalendarCredentialMaterialResolver
{
    public static string Resolve(IntegrationCredentialReference credentialReference)
    {
        ArgumentNullException.ThrowIfNull(credentialReference);

        if (credentialReference.Provider != IntegrationProvider.GoogleCalendar)
        {
            throw NotConfigured();
        }

        if (!string.IsNullOrWhiteSpace(credentialReference.Reference))
        {
            return credentialReference.Reference;
        }

        throw NotConfigured();
    }

    private static BusinessRuleException NotConfigured()
    {
        return new BusinessRuleException(
            "google_calendar_tool_not_configured",
            "Google Calendar tool, configuration, and credential reference are required.");
    }
}
