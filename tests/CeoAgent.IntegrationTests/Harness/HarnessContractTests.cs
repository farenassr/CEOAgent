using System.Text.RegularExpressions;
using Shouldly;

namespace CeoAgent.IntegrationTests.Harness;

public sealed partial class HarnessContractTests
{
    /// <summary>
    /// Verifies that the harness exposes stable scripts for repeated agent workflows.
    /// </summary>
    [Test]
    public void HarnessScripts_ArePresent()
    {
        var repoRoot = FindRepositoryRoot();
        var expectedScripts = new[]
        {
            "architecture-check.ps1",
            "aspire-smoke.ps1",
            "doc-gardening.ps1",
            "harness-check.ps1",
            "review-current-branch.ps1",
            "whatsapp-eval.ps1",
        };

        var missing = expectedScripts
            .Where(script => !File.Exists(Path.Combine(repoRoot, "AIHarness", "scripts", script)))
            .ToArray();

        missing.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that PromptTemplate.md remains ignored scratch material outside the root agent guide.
    /// </summary>
    [Test]
    public void PromptTemplate_IsOnlyReferencedAsIgnoredScratchMaterial()
    {
        var repoRoot = FindRepositoryRoot();
        var violations = Directory.EnumerateFiles(repoRoot, "*.md", SearchOption.AllDirectories)
            .Where(filePath => !IsIgnoredPath(filePath))
            .Where(filePath => Path.GetFileName(filePath) != "PromptTemplate.md")
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains("PromptTemplate.md", StringComparison.Ordinal))
                .Select(item => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1}: {item.line.Trim()}"))
            .Where(match => !match.StartsWith("AGENTS.md:", StringComparison.Ordinal)
                && !match.StartsWith($".codex{Path.DirectorySeparatorChar}AGENTS.md:", StringComparison.Ordinal)
                && !match.StartsWith($".claude{Path.DirectorySeparatorChar}AGENTS.md:", StringComparison.Ordinal))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that agent guides do not point to missing historical instruction files.
    /// </summary>
    [Test]
    public void AgentGuides_DoNotReferenceMissingHistoricalAgentsGuide()
    {
        var repoRoot = FindRepositoryRoot();
        var guideFiles = new[]
        {
            "AGENTS.md",
            Path.Combine(".codex", "AGENTS.md"),
            Path.Combine(".claude", "AGENTS.md"),
        };

        var violations = guideFiles
            .Select(path => Path.Combine(repoRoot, path))
            .Where(File.Exists)
            .SelectMany(filePath => File.ReadLines(filePath)
                .Select((line, index) => new { line, index })
                .Where(item => item.line.Contains(".agents/AGENTS.md", StringComparison.Ordinal)
                    || item.line.Contains(@".agents\AGENTS.md", StringComparison.Ordinal))
                .Select(item => $"{Path.GetRelativePath(repoRoot, filePath)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that WhatsApp eval fixtures are backed by an executable runner.
    /// </summary>
    [Test]
    public void WhatsappEvalFixtures_AreDocumentedAsAutomated()
    {
        var repoRoot = FindRepositoryRoot();
        var whatsappFlow = File.ReadAllText(Path.Combine(repoRoot, "AIHarness", "whatsapp-flow.md"));

        whatsappFlow.ShouldContain("AIHarness/scripts/whatsapp-eval.ps1");
        whatsappFlow.ShouldNotContain("They are not an automated test");
    }

    /// <summary>
    /// Verifies that the harness doc index covers every AIHarness markdown file.
    /// </summary>
    [Test]
    public void HarnessDocIndex_CoversEveryHarnessDocument()
    {
        var repoRoot = FindRepositoryRoot();
        var harnessIndex = File.ReadAllText(Path.Combine(repoRoot, "AIHarness", "harness-engineering.md"));
        var missing = Directory.EnumerateFiles(Path.Combine(repoRoot, "AIHarness"), "*.md")
            .Select(filePath => $"AIHarness/{Path.GetFileName(filePath)}")
            .Where(relativePath => !harnessIndex.Contains(relativePath, StringComparison.Ordinal))
            .ToArray();

        missing.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that PLANS.md tracks active plans, completed plans, debt, and decisions.
    /// </summary>
    [Test]
    public void PlansIndex_HasLifecycleSections()
    {
        var repoRoot = FindRepositoryRoot();
        var plans = File.ReadAllText(Path.Combine(repoRoot, "PLANS.md"));

        plans.ShouldContain("## Active Plans");
        plans.ShouldContain("## Completed Plans");
        plans.ShouldContain("## Technical Debt");
        plans.ShouldContain("## Decision Log");
    }

    /// <summary>
    /// Verifies that local observability smoke checks are documented and scripted.
    /// </summary>
    [Test]
    public void LocalObservabilityHarness_IsDocumentedAndScripted()
    {
        var repoRoot = FindRepositoryRoot();
        var harnessIndex = File.ReadAllText(Path.Combine(repoRoot, "AIHarness", "harness-engineering.md"));

        File.Exists(Path.Combine(repoRoot, "AIHarness", "scripts", "aspire-smoke.ps1")).ShouldBeTrue();
        harnessIndex.ShouldContain("AIHarness/scripts/aspire-smoke.ps1");
        harnessIndex.ShouldContain("health");
        harnessIndex.ShouldContain("logs");
        harnessIndex.ShouldContain("traces");
    }

    /// <summary>
    /// Verifies that local markdown links point to files that exist.
    /// </summary>
    [Test]
    public void MarkdownLinks_PointToExistingLocalFiles()
    {
        var repoRoot = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(repoRoot, "*.md", SearchOption.AllDirectories).Where(filePath => !IsIgnoredPath(filePath)))
        {
            var text = File.ReadAllText(filePath);
            foreach (Match match in MarkdownLinkRegex().Matches(text))
            {
                var target = match.Groups["target"].Value;
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith('#')
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || target.Contains("://", StringComparison.Ordinal))
                {
                    continue;
                }

                var pathOnly = target.Split('#')[0].Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(pathOnly))
                {
                    continue;
                }

                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, pathOnly));
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, filePath)} links to missing {target}");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    private static bool IsIgnoredPath(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || filePath.Contains($"{Path.DirectorySeparatorChar}TestResults{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || filePath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || filePath.Contains($"{Path.DirectorySeparatorChar}.codex{Path.DirectorySeparatorChar}skills{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || filePath.Contains($"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}skills{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
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

    [GeneratedRegex(@"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.None, 100)]
    private static partial Regex MarkdownLinkRegex();
}
