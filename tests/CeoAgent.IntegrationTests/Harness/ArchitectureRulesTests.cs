using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;

namespace CeoAgent.IntegrationTests.Harness;

public sealed partial class ArchitectureRulesTests
{
    private static readonly string[] ProductionProjectDirectories =
    [
        "CeoAgent.ApiService",
        "CeoAgent.AppHost",
        "CeoAgent.Application",
        "CeoAgent.Infrastructure",
        "CeoAgent.ServiceDefaults",
        "CeoAgent.Shared",
        "CeoAgent.Worker",
    ];

    private static readonly Dictionary<string, string[]> AllowedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CeoAgent.ApiService"] =
            [
                "CeoAgent.Application",
                "CeoAgent.Infrastructure",
                "CeoAgent.ServiceDefaults",
                "CeoAgent.Shared",
            ],
            ["CeoAgent.AppHost"] = ["CeoAgent.ApiService", "CeoAgent.Worker"],
            ["CeoAgent.Application"] = ["CeoAgent.Shared"],
            ["CeoAgent.Infrastructure"] = ["CeoAgent.Application", "CeoAgent.Shared"],
            ["CeoAgent.ServiceDefaults"] = [],
            ["CeoAgent.Shared"] = [],
            ["CeoAgent.Worker"] =
            [
                "CeoAgent.Application",
                "CeoAgent.Infrastructure",
                "CeoAgent.ServiceDefaults",
                "CeoAgent.Shared",
            ],
        };

    /// <summary>
    /// Verifies that production project references follow the repository dependency map.
    /// </summary>
    [Test]
    public void ProjectReferences_FollowAllowedDependencyMap()
    {
        var repoRoot = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var projectDirectory in ProductionProjectDirectories)
        {
            var projectPath = Directory.GetFiles(GetProductionProjectRoot(repoRoot, projectDirectory), "*.csproj").Single();
            var document = XDocument.Load(projectPath);
            var references = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetFileNameWithoutExtension(value!))
                .Order(StringComparer.Ordinal)
                .ToArray();

            foreach (var reference in references)
            {
                if (!AllowedProjectReferences[projectDirectory].Contains(reference, StringComparer.Ordinal))
                {
                    violations.Add($"{projectDirectory} references {reference}");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that production code does not import MediatR.
    /// </summary>
    [Test]
    public void ProductionCode_DoesNotUseMediatR()
    {
        FindMatchingSourceLines("MediatR").ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that the API surface is built with FastEndpoints instead of MVC controllers.
    /// </summary>
    [Test]
    public void ApiService_DoesNotUseMvcControllers()
    {
        var matches = FindMatchingSourceLines("ControllerBase")
            .Concat(FindMatchingSourceLines("AddControllers"))
            .Concat(FindMatchingSourceLines("MapControllers"))
            .ToArray();

        matches.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that provider SDK packages and namespaces stay in infrastructure implementation code.
    /// </summary>
    [Test]
    public void ProviderSdkUsage_StaysInsideIntegrationImplementations()
    {
        var repoRoot = FindRepositoryRoot();
        var providerMarkers = new[]
        {
            "Azure.Security.KeyVault.Secrets",
            "Google.Apis",
            "OpenAI.Responses",
            "Microsoft.Agents.AI.OpenAI",
            "Refit",
        };
        var violations = new List<string>();

        foreach (var projectDirectory in ProductionProjectDirectories)
        {
            foreach (var filePath in EnumerateProductionFiles(repoRoot, projectDirectory, ["*.cs", "*.csproj"]))
            {
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                var isInfrastructureImplementation = relativePath.StartsWith(
                    Path.Combine("src", "CeoAgent.Infrastructure", "Implementation") + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal);
                var isInfrastructureApiClient = relativePath.StartsWith(
                    Path.Combine("src", "CeoAgent.Infrastructure", "ApiClient") + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal);
                var isInfrastructureProjectFile = relativePath == Path.Combine(
                    "src",
                    "CeoAgent.Infrastructure",
                    "CEOAgent.Infrastructure.csproj");
                var isApplicationProviderFactory = relativePath == Path.Combine(
                        "src",
                        "CeoAgent.Application",
                        "Abstractions",
                        "AITools",
                        "GoogleCalendar",
                        "IGoogleCalendarServiceFactory.cs")
                    || relativePath == Path.Combine(
                        "src",
                        "CeoAgent.Application",
                        "Abstractions",
                        "OpenAI",
                        "IOpenAIResponsesClientFactory.cs")
                    || relativePath == Path.Combine(
                        "src",
                        "CeoAgent.Application",
                        "CEOAgent.Application.csproj");
                var text = File.ReadAllText(filePath);
                foreach (var marker in providerMarkers)
                {
                    if (!isInfrastructureImplementation
                        && !isInfrastructureApiClient
                        && !isInfrastructureProjectFile
                        && !isApplicationProviderFactory
                        && text.Contains(marker, StringComparison.Ordinal))
                    {
                        violations.Add($"{relativePath} contains {marker}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that FastEndpoints routes are versioned under /v1.
    /// </summary>
    [Test]
    public void FastEndpointsRoutes_AreVersioned()
    {
        var repoRoot = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var filePath in EnumerateProductionFiles(repoRoot, "CeoAgent.ApiService", ["*.cs"]))
        {
            var lines = File.ReadAllLines(filePath);
            for (var index = 0; index < lines.Length; index++)
            {
                var match = FastEndpointsRouteRegex().Match(lines[index]);
                if (!match.Success)
                {
                    continue;
                }

                var route = match.Groups["route"].Value;
                if (!route.StartsWith("/v1/", StringComparison.Ordinal) && route != "/health")
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{index + 1} uses {route}");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that critical contract names are not split across multiple CLR namespaces.
    /// </summary>
    [Test]
    public void CriticalContracts_DoNotHaveDuplicateTypeDefinitions()
    {
        var repoRoot = FindRepositoryRoot();
        var monitoredTypeNames = new[]
        {
            "ICompanyContext",
            "ICompanyContextAccessor",
            "ICompanyOwned",
        };
        var declarations = FindPublicTypeDeclarations(repoRoot)
            .Where(declaration => monitoredTypeNames.Contains(declaration.TypeName, StringComparer.Ordinal))
            .GroupBy(declaration => declaration.TypeName, StringComparer.Ordinal)
            .Select(group => new
            {
                TypeName = group.Key,
                Declarations = group
                    .Select(declaration => $"{declaration.Namespace}.{declaration.TypeName} in {declaration.RelativePath}")
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(group => group.Declarations.Length > 1)
            .Select(group => $"{group.TypeName}: {string.Join("; ", group.Declarations)}")
            .ToArray();

        declarations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that infrastructure implementation code stays in the approved target folders.
    /// </summary>
    [Test]
    public void InfrastructureImplementationFiles_FollowTargetFolderConvention()
    {
        var repoRoot = FindRepositoryRoot();
        var implementationRoot = Path.Combine(repoRoot, "src", "CeoAgent.Infrastructure", "Implementation");
        var allowedTopLevelFolders = new HashSet<string>(StringComparer.Ordinal)
        {
            "AI",
            "AITools",
            "Company",
            "GoogleCalendar",
            "Messaging",
            "OpenAI",
            "Secrets",
        };

        var violations = Directory.EnumerateFiles(implementationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => !filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(filePath => new
            {
                FilePath = filePath,
                RelativeParts = Path.GetRelativePath(implementationRoot, filePath)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            })
            .Where(item => item.RelativeParts.Length < 2 || !allowedTopLevelFolders.Contains(item.RelativeParts[0]))
            .Select(item => Path.GetRelativePath(repoRoot, item.FilePath))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that files moved under approved physical folders declare the matching namespace.
    /// </summary>
    [Test]
    public void OrganizedRuntimeFiles_UseNamespacesThatMatchTheirFolders()
    {
        var repoRoot = FindRepositoryRoot();
        var namespaceRoots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Path.Combine("src", "CeoAgent.Application", "Abstractions", "Company")] = "CeoAgent.Application.Abstractions.Company",
            [Path.Combine("src", "CeoAgent.Application", "Abstractions")] = "CeoAgent.Application.Abstractions",
            [Path.Combine("src", "CeoAgent.Shared", "AI")] = "CeoAgent.Shared.AI",
            [Path.Combine("src", "CeoAgent.Shared", "AITools")] = "CeoAgent.Shared.AITools",
            [Path.Combine("src", "CeoAgent.Shared", "Calendar")] = "CeoAgent.Shared.Calendar",
            [Path.Combine("src", "CeoAgent.Shared", "Jobs")] = "CeoAgent.Shared.Jobs",
            [Path.Combine("src", "CeoAgent.Shared", "Messaging")] = "CeoAgent.Shared.Messaging",
            [Path.Combine("src", "CeoAgent.Infrastructure", "Entities", "Filters")] = "CeoAgent.Infrastructure.Entities.Filters",
            [Path.Combine("src", "CeoAgent.Infrastructure", "Implementation")] = "CeoAgent.Infrastructure.Implementation",
        };
        var enforcedFolders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Abstractions",
            "Implementations",
            "Implementation",
            "Models",
        };
        var violations = new List<string>();

        foreach (var (relativeRoot, namespaceRoot) in namespaceRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, relativeRoot);
            foreach (var filePath in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(filePath => !filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                var relativeParts = Path.GetRelativePath(absoluteRoot, filePath)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (relativeParts.Length < 2 || !enforcedFolders.Contains(relativeParts[0]))
                {
                    continue;
                }

                var expectedNamespace = namespaceRoot + "." + string.Join(
                    ".",
                    relativeParts.Take(relativeParts.Length - 1));
                var text = File.ReadAllText(filePath);
                var namespaceMatch = NamespaceRegex().Match(text);
                var actualNamespace = namespaceMatch.Success ? namespaceMatch.Groups["namespace"].Value : "<missing>";
                if (!string.Equals(actualNamespace, expectedNamespace, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, filePath)} declares {actualNamespace}; expected {expectedNamespace}");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that PostgreSQL 18 uses a fresh local data volume instead of the previous PostgreSQL 17 volume.
    /// </summary>
    [Test]
    public void AppHost_Postgres18_UsesVersionSpecificDataVolume()
    {
        var repoRoot = FindRepositoryRoot();
        var appHost = File.ReadAllText(Path.Combine(repoRoot, "src", "CeoAgent.AppHost", "AppHost.cs"));
        appHost.ShouldContain(".WithDataVolume(\"ceoagent-postgres-database-volume\")");
    }

    private static string[] FindMatchingSourceLines(string marker)
    {
        var repoRoot = FindRepositoryRoot();
        return ProductionProjectDirectories
            .SelectMany(projectDirectory => EnumerateProductionFiles(repoRoot, projectDirectory, ["*.cs", "*.csproj"]))
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains(marker, StringComparison.Ordinal))
                .Select(item => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();
    }

    private static IEnumerable<string> EnumerateProductionFiles(string repoRoot, string projectDirectory, string[] searchPatterns)
    {
        var root = GetProductionProjectRoot(repoRoot, projectDirectory);
        foreach (var pattern in searchPatterns)
        {
            foreach (var filePath in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                         .Where(filePath => !filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                yield return filePath;
            }
        }
    }

    private static IEnumerable<PublicTypeDeclaration> FindPublicTypeDeclarations(string repoRoot)
    {
        foreach (var filePath in ProductionProjectDirectories
                     .SelectMany(projectDirectory => EnumerateProductionFiles(repoRoot, projectDirectory, ["*.cs"])))
        {
            var text = File.ReadAllText(filePath);
            var namespaceMatch = NamespaceRegex().Match(text);
            if (!namespaceMatch.Success)
            {
                continue;
            }

            foreach (Match typeMatch in PublicTypeRegex().Matches(text))
            {
                yield return new PublicTypeDeclaration(
                    namespaceMatch.Groups["namespace"].Value,
                    typeMatch.Groups["name"].Value,
                    Path.GetRelativePath(repoRoot, filePath));
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CEOAgent.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static string GetProductionProjectRoot(string repoRoot, string projectDirectory)
    {
        return Path.Combine(repoRoot, "src", projectDirectory);
    }

    [GeneratedRegex("\\b(?:Get|Post|Put|Patch|Delete)\\(\\\"(?<route>[^\\\"]+)\\\"", RegexOptions.None, 100)]
    private static partial Regex FastEndpointsRouteRegex();

    [GeneratedRegex("namespace\\s+(?<namespace>[A-Za-z0-9_.]+)\\s*;", RegexOptions.None, 100)]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex("(?m)^public\\s+(?:sealed\\s+|static\\s+|abstract\\s+|partial\\s+)*?(?:interface|class|record|enum)\\s+(?<name>[A-Za-z0-9_]+)", RegexOptions.None, 100)]
    private static partial Regex PublicTypeRegex();

    private sealed record PublicTypeDeclaration(string Namespace, string TypeName, string RelativePath);
}
