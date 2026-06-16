using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class ApiProjectConfigurationTests
{
    [Test]
    public void Program_AppliesAuthenticationBeforeAuthorization()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "CeoAgent.ApiService",
            "Program.cs"));
        var program = File.ReadAllText(programPath);

        program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal)
            .ShouldBeLessThan(program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal));
    }
}
