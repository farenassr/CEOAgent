using CeoAgent.Adapters.Secrets;
using CeoAgent.Adapters.GoogleCalendar.Abstractions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;

namespace CeoAgent.Adapters.GoogleCalendar.Service;

public sealed class GoogleCalendarServiceFactory(ISecretValueProvider secrets)
    : IGoogleCalendarServiceFactory
{
    public static readonly IReadOnlyList<string> Scopes =
    [
        CalendarService.ScopeConstants.CalendarFreebusy,
        CalendarService.ScopeConstants.CalendarEvents,
    ];

    public async Task<CalendarService> CreateAsync(
        string credentialReference,
        CancellationToken cancellationToken)
    {
        var serviceAccountJson = IsServiceAccountJson(credentialReference)
            ? credentialReference
            : await secrets.GetSecretValueAsync(credentialReference, cancellationToken);

        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson)
            .ToGoogleCredential()
            .CreateScoped(Scopes);

        return new CalendarService(new BaseClientService.Initializer
        {
            ApplicationName = "CEOAgent",
            HttpClientInitializer = credential,
        });
    }

    private static bool IsServiceAccountJson(string value)
    {
        return value.TrimStart().StartsWith('{');
    }
}
