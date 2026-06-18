using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Infrastructure.Persistence;

internal static class DatabaseMigrationStartupExtensions
{
    public static async Task ApplyConfiguredDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var persistenceOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<PersistenceOptions>>()
            .Value;

        if (!persistenceOptions.ApplyMigrationsOnStartup || persistenceOptions.UseInMemoryDatabase)
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
