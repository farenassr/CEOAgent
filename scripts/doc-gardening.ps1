[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$violations = [System.Collections.Generic.List[string]]::new()

function Add-Violation {
    param([string]$Message)
    $violations.Add($Message)
}

function Get-RepoRelativePath {
    param([string]$Path)

    $root = (Resolve-Path -LiteralPath $repoRoot).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    return $fullPath.Substring($root.Length + 1)
}

function Test-IgnoredMarkdownPath {
    param([string]$Path)

    $relativePath = Get-RepoRelativePath -Path $Path
    return $relativePath -match "^(bin|obj|TestResults|\.git)[\\/]" -or
        $relativePath -match "^\.codex[\\/]skills[\\/]" -or
        $relativePath -match "[\\/]bin[\\/]|[\\/]obj[\\/]|[\\/]TestResults[\\/]"
}

$markdownFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter "*.md" |
    Where-Object { -not (Test-IgnoredMarkdownPath -Path $_.FullName) }

foreach ($file in $markdownFiles) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $content = Get-Content -LiteralPath $file.FullName -Raw

    if ($content.Contains(".agents/AGENTS.md") -or $content.Contains(".agents\AGENTS.md")) {
        Add-Violation "$relativePath references missing .agents/AGENTS.md"
    }

    foreach ($match in [regex]::Matches($content, '\[[^\]]+\]\((?<target>[^)]+)\)', [System.Text.RegularExpressions.RegexOptions]::None, [timespan]::FromMilliseconds(100))) {
        $target = $match.Groups["target"].Value
        if ($target.StartsWith("http", [System.StringComparison]::OrdinalIgnoreCase) -or
            $target.StartsWith("#") -or
            $target.StartsWith("mailto:", [System.StringComparison]::OrdinalIgnoreCase) -or
            $target.Contains("://")) {
            continue
        }

        $pathOnly = $target.Split("#")[0].Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        if ([string]::IsNullOrWhiteSpace($pathOnly)) {
            continue
        }

        $resolved = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $pathOnly))
        if (-not (Test-Path -LiteralPath $resolved)) {
            Add-Violation "$relativePath links to missing $target"
        }
    }
}

$harnessIndex = Get-Content -LiteralPath (Join-Path $repoRoot "AIHarness/harness-engineering.md") -Raw
foreach ($harnessDoc in Get-ChildItem -LiteralPath (Join-Path $repoRoot "AIHarness") -File -Filter "*.md") {
    $relativePath = "AIHarness/$($harnessDoc.Name)"
    if (-not $harnessIndex.Contains($relativePath)) {
        Add-Violation "AIHarness/harness-engineering.md does not index $relativePath"
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Doc gardening check failed:" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host " - $violation" -ForegroundColor Red
    }

    exit 1
}

Write-Host "Doc gardening check passed."
