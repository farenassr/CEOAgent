using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CEOAgent.Tests.Support;

internal sealed class ApiFactory(string environmentName = "Testing", string? databaseName = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseSetting("Persistence:UseInMemoryDatabase", "true");
        builder.UseSetting("Persistence:InMemoryDatabaseName", databaseName ?? $"api-tests-{Guid.CreateVersion7()}");
    }
}
