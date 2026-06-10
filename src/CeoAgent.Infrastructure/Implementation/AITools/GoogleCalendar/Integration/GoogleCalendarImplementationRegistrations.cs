using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using Google.Apis.Calendar.v3;
using Microsoft.Extensions.DependencyInjection;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;

internal static class GoogleCalendarImplementationRegistrations
{
    public static IServiceCollection AddGoogleCalendarImplementation(this IServiceCollection services)
    {
        services.AddScoped<IGoogleCalendarServiceFactory<CalendarService>, GoogleCalendarServiceFactory>();
        services.AddScoped<IGoogleCalendarIntegration, GoogleCalendarIntegration>();
        return services;
    }
}
