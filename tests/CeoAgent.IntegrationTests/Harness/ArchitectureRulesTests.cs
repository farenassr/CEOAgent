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
                var text = File.ReadAllText(filePath);
                foreach (var marker in providerMarkers)
                {
                    if (!isInfrastructureImplementation
                        && !isInfrastructureApiClient
                        && !isInfrastructureProjectFile
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
            "IOrganizationContextProvider",
            "IOrganizationContextAccessor",
            "IOrganizationOwned",
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

    [Test]
    public void ProductionEnums_LiveInSharedProject()
    {
        var repoRoot = FindRepositoryRoot();
        var violations = FindPublicTypeDeclarations(repoRoot)
            .Where(declaration => declaration.Kind == "enum")
            .Where(declaration => !declaration.RelativePath.StartsWith(
                Path.Combine("src", "CeoAgent.Shared") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .Select(declaration => $"{declaration.TypeName} in {declaration.RelativePath}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.ShouldBeEmpty();
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
            "Organization",
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
            [Path.Combine("src", "CeoAgent.Application", "Abstractions", "Organization")] = "CeoAgent.Application.Abstractions.Organization",
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
    public void AppHost_Postgres_UsesStableDataVolume()
    {
        var repoRoot = FindRepositoryRoot();
        var ceoAgentApplication = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "CeoAgentApplicationExtensions.cs"));

        ceoAgentApplication.ShouldContain(".WithDataVolume(\"ceoagent-postgres-database-volume\")");
    }

    /// <summary>
    /// Verifies that Aspire dashboard resource links for the API open the Scalar reference directly.
    /// </summary>
    [Test]
    public void AppHost_ApiResourceLinks_DeepLinkToScalar()
    {
        var repoRoot = FindRepositoryRoot();
        var ceoAgentApplication = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "CeoAgentApplicationExtensions.cs"));

        ceoAgentApplication.ShouldContain(".WithUrlForEndpoint(\"https\", url =>");
        ceoAgentApplication.ShouldContain(".WithUrlForEndpoint(\"http\", url =>");
        ceoAgentApplication.ShouldContain("url.DisplayText = \"Scalar API Reference\";");
        ceoAgentApplication.ShouldContain("url.Url = \"/scalar\";");
    }

    [Test]
    public void AppHost_RuntimePortsAndPostgresSettings_ComeFromAppSettings()
    {
        var repoRoot = FindRepositoryRoot();
        var appHost = File.ReadAllText(Path.Combine(repoRoot, "src", "CeoAgent.AppHost", "AppHost.cs"));
        var appHostOptions = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "AppHostOptions.cs"));
        var ceoAgentApplication = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "CeoAgentApplicationExtensions.cs"));
        var providerEnvironment = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "ProviderEnvironmentExtensions.cs"));
        var appHostSettings = File.ReadAllText(Path.Combine(repoRoot, "src", "CeoAgent.AppHost", "appsettings.json"));

        appHost.ShouldNotContain("const int PgAdminHostPort");
        appHost.ShouldNotContain("const int PostgresHostPort");
        appHost.ShouldNotContain("const int ApiServiceHttpsHostPort");
        appHost.ShouldNotContain("const int ApiServiceHttpHostPort");
        appHost.ShouldNotContain("const int AzuriteBlobPort");
        appHost.ShouldNotContain("const int AzuriteQueuePort");
        appHost.ShouldNotContain("const int AzuriteTablePort");
        appHost.ShouldNotContain("const string PostgresResourceName");
        appHost.ShouldNotContain("const string PostgresUsername");
        appHost.ShouldNotContain("const string PostgresPasswordSecretName");
        appHost.ShouldContain("var options = builder.Configuration.GetRequiredAppHostOptions();");
        appHost.ShouldContain("builder.AddCeoAgentApplication(options);");

        appHostOptions.ShouldContain("internal sealed class AppHostOptions");
        appHostOptions.ShouldContain("public ResourceNameOptions Resources");
        appHostOptions.ShouldContain("public PostgresOptions Postgres");
        appHostOptions.ShouldContain("public static AppHostOptions GetRequiredAppHostOptions");
        appHostOptions.ShouldContain("Require(options.Resources.Storage, \"Resources:Storage\")");
        appHostOptions.ShouldContain("Require(options.Postgres.ResourceName, \"Postgres:ResourceName\")");
        appHostOptions.ShouldContain("Require(options.Postgres.Host, \"Postgres:Host\")");
        appHostOptions.ShouldContain("RequirePositive(options.Postgres.Port, \"Postgres:Port\")");
        appHostOptions.ShouldContain("RequirePositive(options.Postgres.HostPort, \"Postgres:HostPort\")");
        appHostOptions.ShouldContain("Require(options.Postgres.Username, \"Postgres:Username\")");
        appHostOptions.ShouldContain("Require(options.Postgres.PasswordSecretName, \"Postgres:PasswordSecretName\")");

        ceoAgentApplication.ShouldContain("options.Resources.Storage");
        ceoAgentApplication.ShouldContain("options.Resources.Queues");
        ceoAgentApplication.ShouldContain("options.Resources.Blobs");
        ceoAgentApplication.ShouldContain("options.ApiService.HttpsHostPort");
        ceoAgentApplication.ShouldContain("options.ApiService.HttpHostPort");
        ceoAgentApplication.ShouldContain("options.Azurite.BlobPort");
        ceoAgentApplication.ShouldContain("options.Azurite.QueuePort");
        ceoAgentApplication.ShouldContain("options.Azurite.TablePort");
        ceoAgentApplication.ShouldContain("AddPostgresConnectionEnvironment(apiService, publishKeyVault, options.Postgres)");
        ceoAgentApplication.ShouldContain("AddPostgresConnectionEnvironment(worker, publishKeyVault, options.Postgres)");
        ceoAgentApplication.ShouldContain("apiService.WaitFor(postgresDatabase)");
        ceoAgentApplication.ShouldContain("worker.WaitFor(postgresDatabase)");
        providerEnvironment.ShouldContain("var applyApiMigrationsOnStartup = ShouldApplyApiMigrationsOnStartup(deploymentEnvironmentName) ? \"true\" : \"false\";");
        providerEnvironment.ShouldContain(".WithEnvironment(\"Persistence__ApplyMigrationsOnStartup\", applyApiMigrationsOnStartup)");
        providerEnvironment.ShouldContain("string.Equals(deploymentEnvironmentName, \"Dev\", StringComparison.OrdinalIgnoreCase)");
        providerEnvironment.ShouldContain("string.Equals(deploymentEnvironmentName, \"Tst\", StringComparison.OrdinalIgnoreCase)");
        (providerEnvironment.Split("Persistence__ApplyMigrationsOnStartup", StringSplitOptions.None).Length - 1).ShouldBe(1);

        appHostSettings.ShouldContain("\"Resources\"");
        appHostSettings.ShouldContain("\"Storage\": \"storage\"");
        appHostSettings.ShouldContain("\"Queues\": \"queues\"");
        appHostSettings.ShouldContain("\"Blobs\": \"blobs\"");
        appHostSettings.ShouldContain("\"HostPort\": 5050");
        appHostSettings.ShouldContain("\"ResourceName\": \"postgres\"");
        appHostSettings.ShouldContain("\"Host\": \"postgres\"");
        appHostSettings.ShouldContain("\"Port\": 5432");
        appHostSettings.ShouldContain("\"HostPort\": 55432");
        appHostSettings.ShouldContain("\"Username\": \"postgres\"");
        appHostSettings.ShouldContain("\"PasswordSecretName\": \"PostgresPassword\"");
        appHostSettings.ShouldContain("\"HttpsHostPort\": 7584");
        appHostSettings.ShouldContain("\"HttpHostPort\": 5481");
        appHostSettings.ShouldContain("\"BlobPort\": 10000");
        appHostSettings.ShouldContain("\"QueuePort\": 10001");
        appHostSettings.ShouldContain("\"TablePort\": 10002");
    }

    [Test]
    public void KeycloakConfiguration_UsesApiAndServiceClientsOnly()
    {
        var repoRoot = FindRepositoryRoot();
        var appHostSettings = File.ReadAllText(Path.Combine(repoRoot, "src", "CeoAgent.AppHost", "appsettings.json"));
        var apiFactory = File.ReadAllText(Path.Combine(repoRoot, "tests", "CeoAgent.ApiService.Tests", "Support", "ApiFactory.cs"));
        var ceoAgentApplication = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "CeoAgentApplicationExtensions.cs"));
        var keycloakEnvironment = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CeoAgent.AppHost",
            "Configuration",
            "KeycloakEnvironmentExtensions.cs"));

        appHostSettings.ShouldContain("\"ClientId\": \"ceo-agent-api\"");
        appHostSettings.ShouldContain("\"ServiceClientId\": \"ceo-agent-service\"");
        appHostSettings.ShouldContain("\"AuthorizationScopes\"");
        appHostSettings.ShouldContain("\"organization\"");
        apiFactory.ShouldContain("builder.UseSetting(\"Keycloak:ClientId\", \"ceo-agent-api\")");
        ceoAgentApplication.ShouldContain("builder.AddKeycloakEnvironment(apiService, keyVault);");
        keycloakEnvironment.ShouldContain(".WithEnvironment(\"Keycloak__ServiceClientId\"");
        keycloakEnvironment.ShouldContain("keycloak-service-client-secret");

        FindMatchingSourceLines("ceo-agent-web").ShouldBeEmpty();
    }

    [Test]
    public void Migrations_DoNotContainSecretShapedCredentialMetadata()
    {
        var repoRoot = FindRepositoryRoot();
        var bannedMarkers = new[]
        {
            "private_key",
            "private_key_id",
            "client_email",
            "access_token",
            "refresh_token",
            "service_account_json",
        };
        var migrationRoot = Path.Combine(repoRoot, "src", "CeoAgent.Infrastructure", "Persistence", "Migrations");
        var violations = Directory.EnumerateFiles(migrationRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .SelectMany(item => bannedMarkers
                    .Where(marker => item.line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    .Select(marker => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1} contains {marker}")))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Test]
    public void OperationalLogs_UseSourceGeneratedLoggerMessages()
    {
        var repoRoot = FindRepositoryRoot();
        var monitoredProjects = new[]
        {
            "CeoAgent.ApiService",
            "CeoAgent.Infrastructure",
            "CeoAgent.Worker",
        };
        var bannedPatterns = new[]
        {
            ".LogInformation(",
            ".LogWarning(",
            ".LogError(",
            ".ZLogInformation(",
            ".ZLogWarning(",
            ".ZLogError(",
        };

        var violations = monitoredProjects
            .SelectMany(projectDirectory => EnumerateProductionFiles(repoRoot, projectDirectory, ["*.cs"]))
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .Where(item => bannedPatterns.Any(pattern => item.line.Contains(pattern, StringComparison.Ordinal)))
                .Select(item => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Test]
    public void WhatsAppLoggerMessages_LiveInModuleLoggingFolder()
    {
        var repoRoot = FindRepositoryRoot();
        var moduleRoot = Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp");
        var loggingRoot = Path.Combine(moduleRoot, "Logging") + Path.DirectorySeparatorChar;
        var violations = Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => !filePath.StartsWith(loggingRoot, StringComparison.Ordinal))
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains("[LoggerMessage(", StringComparison.Ordinal))
                .Select(item => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Test]
    public void OperationalLogEventIds_UseStableRanges()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceText = string.Join(
            Environment.NewLine,
            new[]
            {
                Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Endpoints", "SendWhatsAppMessageEndpoint.cs"),
                Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Endpoints", "WhatsAppWebhookEndpoint.cs"),
                Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Services", "WhatsAppWebhookIngestionService.cs"),
                Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Services", "IncomingMessageOutboxDispatcher.cs"),
                Path.Combine(repoRoot, "src", "CeoAgent.Worker", "Jobs", "IncomingMessageQueueWorker.cs"),
            }
            .Concat(Directory.Exists(Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Logging"))
                ? Directory.EnumerateFiles(Path.Combine(repoRoot, "src", "CeoAgent.ApiService", "Modules", "WhatsApp", "Logging"), "*.cs", SearchOption.AllDirectories)
                : [])
            .Concat(Directory.Exists(Path.Combine(repoRoot, "src", "CeoAgent.Worker", "Jobs", "Logging"))
                ? Directory.EnumerateFiles(Path.Combine(repoRoot, "src", "CeoAgent.Worker", "Jobs", "Logging"), "*.cs", SearchOption.AllDirectories)
                : [])
            .Select(File.ReadAllText));

        sourceText.ShouldContain("EventId = 4101");
        sourceText.ShouldContain("WhatsAppManualMessageSent");
        sourceText.ShouldContain("EventId = 4102");
        sourceText.ShouldContain("WhatsAppManualMessageSendFailed");
        sourceText.ShouldContain("EventId = 4201");
        sourceText.ShouldContain("WhatsAppWebhookReceived");
        sourceText.ShouldContain("EventId = 4202");
        sourceText.ShouldContain("WhatsAppWebhookMessagePersisted");
        sourceText.ShouldContain("EventId = 4203");
        sourceText.ShouldContain("WhatsAppWebhookMessageEnqueued");
        sourceText.ShouldContain("EventId = 2101");
        sourceText.ShouldContain("IncomingMessageOutboxDispatchSucceeded");
        sourceText.ShouldContain("EventId = 2102");
        sourceText.ShouldContain("IncomingMessageOutboxDispatchFailed");
        sourceText.ShouldContain("EventId = 2201");
        sourceText.ShouldContain("IncomingQueueMessageProcessed");
        sourceText.ShouldContain("EventId = 2202");
        sourceText.ShouldContain("IncomingQueueMessageFailed");
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
                    typeMatch.Groups["kind"].Value,
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

    [GeneratedRegex("(?m)^public\\s+(?:sealed\\s+|static\\s+|abstract\\s+|partial\\s+)*?(?<kind>interface|class|record|enum)\\s+(?<name>[A-Za-z0-9_]+)", RegexOptions.None, 100)]
    private static partial Regex PublicTypeRegex();

    private sealed record PublicTypeDeclaration(string Namespace, string Kind, string TypeName, string RelativePath);
}
