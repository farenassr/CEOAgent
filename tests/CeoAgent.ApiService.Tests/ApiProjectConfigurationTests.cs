using System.Xml.Linq;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class ApiProjectConfigurationTests
{
    [Test]
    public void ApiServiceProject_DefinesUserSecretsIdForLocalGoogleCalendarCredentials()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "CeoAgent.ApiService",
            "CeoAgent.ApiService.csproj"));
        var document = XDocument.Load(projectPath);

        var userSecretsId = document
            .Descendants("UserSecretsId")
            .SingleOrDefault()
            ?.Value;

        userSecretsId.ShouldNotBeNullOrWhiteSpace();
    }
}
