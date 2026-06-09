using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Infrastructure.Implementation.Secrets;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;

public sealed class GoogleCalendarServiceFactory(ISecretValueProvider secrets, IMemoryCache cache)
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
        var cacheKey = $"GoogleCalendarService:{HashCacheKey(credentialReference)}";
        if (cache.TryGetValue<CalendarService>(cacheKey, out var cachedService) && cachedService is not null)
        {
            return cachedService;
        }

        if (IsServiceAccountJson(credentialReference))
        {
            throw new InvalidOperationException("Google Calendar credential reference must not contain inline credential JSON.");
        }

        var serviceAccountJson = await secrets.GetSecretValueAsync(credentialReference, cancellationToken);

        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson)
            .ToGoogleCredential()
            .CreateScoped(Scopes);

        var service = new CalendarService(new BaseClientService.Initializer
        {
            ApplicationName = "CEOAgent",
            HttpClientInitializer = credential,
        });

        service.HttpClient.Timeout = TimeSpan.FromSeconds(30);

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        }.RegisterPostEvictionCallback(static (_, value, _, _) =>
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        });
        cache.Set(cacheKey, service, cacheEntryOptions);

        return service;
    }

    private static string HashCacheKey(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private static bool IsServiceAccountJson(string value)
    {
        return value.TrimStart().StartsWith('{');
    }
}
