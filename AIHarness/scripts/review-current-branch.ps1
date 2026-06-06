[CmdletBinding()]
param(
    [string]$BaseBranch = "",
    [string]$OutputPath = "docs/CODE_REVIEW/current-branch-review-context.md"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($BaseBranch)) {
        $candidateBranches = @("origin/main", "origin/master", "main", "master")
        $BaseBranch = $candidateBranches | Where-Object {
            git rev-parse --verify $_ 2>$null
        } | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($BaseBranch)) {
        throw "Could not determine base branch. Pass -BaseBranch explicitly."
    }

    $resolvedOutputPath = Join-Path $repoRoot $OutputPath
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $status = git status --short
    $nameStatus = git diff --name-status $BaseBranch
    $diffStat = git diff --stat $BaseBranch

    $content = @(
        "# Current Branch Review Context",
        "",
        "Base branch: $BaseBranch",
        "Generated: $(Get-Date -Format o)",
        "",
        "## Git Status",
        "```text",
        ($status -join [Environment]::NewLine),
        "```",
        "",
        "## Changed Files",
        "```text",
        ($nameStatus -join [Environment]::NewLine),
        "```",
        "",
        "## Diff Stat",
        "```text",
        ($diffStat -join [Environment]::NewLine),
        "```",
        "",
        "## Review Prompt",
        "Use docs/reviewer.md to review only the current branch diff against the base branch above."
    )

    Set-Content -LiteralPath $resolvedOutputPath -Value $content -Encoding utf8
    Write-Host "Review context written to $OutputPath"
}
finally {
    Pop-Location
}
