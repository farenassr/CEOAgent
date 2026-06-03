using CeoAgent.Tools.Implementation.Execution;
using CeoAgent.Tools.Implementation.GoogleCalendar;
using Microsoft.Extensions.DependencyInjection;

namespace CeoAgent.Tools;

public static class ToolsRegistrations
{
    public static IServiceCollection AddToolsRuntime(this IServiceCollection services)
    {
        services.AddScoped<CompanyToolRegistry>();
        services.AddScoped<GoogleCalendarToolExecutor>();
        services.AddScoped<ToolExecutionGateway>();

        return services;
    }
}
