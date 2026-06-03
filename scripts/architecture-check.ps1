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

function Get-ProductionFile {
    param(
        [string]$ProjectDirectory,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        Get-ChildItem -LiteralPath (Join-Path $repoRoot $ProjectDirectory) -Recurse -File -Filter $pattern |
            Where-Object {
                $_.FullName -notmatch [regex]::Escape("\bin\") -and
                $_.FullName -notmatch [regex]::Escape("\obj\")
            }
    }
}

$productionProjects = @(
    "CeoAgent.Adapters",
    "CeoAgent.ApiService",
    "CeoAgent.AppHost",
    "CeoAgent.Application",
    "CeoAgent.Infrastructure",
    "CeoAgent.Integrations",
    "CeoAgent.ServiceDefaults",
    "CeoAgent.Shared",
    "CeoAgent.Tools",
    "CeoAgent.Worker"
)

$allowedProjectReferences = @{
    "CeoAgent.Adapters" = @("CeoAgent.Integrations")
    "CeoAgent.ApiService" = @("CeoAgent.Adapters", "CeoAgent.Application", "CeoAgent.Infrastructure", "CeoAgent.Integrations", "CeoAgent.ServiceDefaults", "CeoAgent.Shared", "CeoAgent.Tools")
    "CeoAgent.AppHost" = @("CeoAgent.ApiService", "CeoAgent.Worker")
    "CeoAgent.Application" = @("CeoAgent.Integrations")
    "CeoAgent.Infrastructure" = @("CeoAgent.Application", "CeoAgent.Shared")
    "CeoAgent.Integrations" = @("CeoAgent.Shared")
    "CeoAgent.ServiceDefaults" = @()
    "CeoAgent.Shared" = @()
    "CeoAgent.Tools" = @("CeoAgent.Application", "CeoAgent.Infrastructure", "CeoAgent.Integrations")
    "CeoAgent.Worker" = @("CeoAgent.Adapters", "CeoAgent.Application", "CeoAgent.Infrastructure", "CeoAgent.Integrations", "CeoAgent.ServiceDefaults", "CeoAgent.Tools")
}

foreach ($projectDirectory in $productionProjects) {
    $projectFile = Get-ChildItem -LiteralPath (Join-Path $repoRoot $projectDirectory) -File -Filter "*.csproj" | Select-Object -First 1
    [xml]$projectXml = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($reference in $projectXml.SelectNodes("//ProjectReference")) {
        $referenceProject = [System.IO.Path]::GetFileNameWithoutExtension($reference.Include)
        if ($allowedProjectReferences[$projectDirectory] -notcontains $referenceProject) {
            Add-Violation "$projectDirectory references $referenceProject"
        }
    }
}

foreach ($file in Get-ProductionFile -ProjectDirectory "." -Patterns @("*.cs", "*.csproj")) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    if ($relativePath -match "^(bin|obj|tests|TestResults|\.git)[\\/]" -or $relativePath -match "[\\/]bin[\\/]|[\\/]obj[\\/]") {
        continue
    }

    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content.Contains("MediatR")) {
        Add-Violation "$relativePath contains MediatR"
    }
}

foreach ($file in Get-ProductionFile -ProjectDirectory "CeoAgent.ApiService" -Patterns @("*.cs")) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($marker in @("ControllerBase", "AddControllers", "MapControllers")) {
        if ($content.Contains($marker)) {
            Add-Violation "$relativePath contains $marker"
        }
    }

    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        $match = [regex]::Match($line, '\b(?:Get|Post|Put|Patch|Delete)\("(?<route>[^"]+)"')
        if ($match.Success) {
            $route = $match.Groups["route"].Value
            if (-not $route.StartsWith("/v1/") -and $route -ne "/health") {
                Add-Violation "${relativePath}:$lineNumber uses unversioned route $route"
            }
        }
    }
}

$providerMarkers = @("Google.Apis", "OpenAI.Responses", "Microsoft.Agents.AI.OpenAI")
foreach ($projectDirectory in $productionProjects | Where-Object { $_ -ne "CeoAgent.Adapters" }) {
    foreach ($file in Get-ProductionFile -ProjectDirectory $projectDirectory -Patterns @("*.cs", "*.csproj")) {
        $relativePath = Get-RepoRelativePath -Path $file.FullName
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($marker in $providerMarkers) {
            if ($content.Contains($marker)) {
                Add-Violation "$relativePath contains provider SDK marker $marker"
            }
        }
    }
}

$monitoredContractNames = @("ICompanyContext", "ICompanyContextAccessor", "ICompanyOwned")
$contractDeclarations = @{}
foreach ($projectDirectory in $productionProjects) {
    foreach ($file in Get-ProductionFile -ProjectDirectory $projectDirectory -Patterns @("*.cs")) {
        $relativePath = Get-RepoRelativePath -Path $file.FullName
        $content = Get-Content -LiteralPath $file.FullName -Raw
        $namespaceMatch = [regex]::Match($content, "namespace\s+(?<namespace>[A-Za-z0-9_.]+)\s*;")
        if (-not $namespaceMatch.Success) {
            continue
        }

        foreach ($typeMatch in [regex]::Matches($content, "(?m)^public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*?(?:interface|class|record|enum)\s+(?<name>[A-Za-z0-9_]+)")) {
            $typeName = $typeMatch.Groups["name"].Value
            if ($monitoredContractNames -notcontains $typeName) {
                continue
            }

            if (-not $contractDeclarations.ContainsKey($typeName)) {
                $contractDeclarations[$typeName] = [System.Collections.Generic.List[string]]::new()
            }

            $contractDeclarations[$typeName].Add("$($namespaceMatch.Groups["namespace"].Value).$typeName in $relativePath")
        }
    }
}

foreach ($typeName in $contractDeclarations.Keys) {
    if ($contractDeclarations[$typeName].Count -gt 1) {
        Add-Violation "$typeName has duplicate definitions: $($contractDeclarations[$typeName] -join '; ')"
    }
}

$toolsRoot = Join-Path $repoRoot "CeoAgent.Tools"
$resolvedToolsRoot = (Resolve-Path -LiteralPath $toolsRoot).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$allowedToolsRootFiles = @("ToolsAssembly.cs", "ToolsRegistrations.cs")
$allowedToolsTopLevelFolders = @("Abstractions", "Implementation", "Models")
foreach ($file in Get-ProductionFile -ProjectDirectory "CeoAgent.Tools" -Patterns @("*.cs")) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $fullPath = (Resolve-Path -LiteralPath $file.FullName).Path
    $relativeToTools = $fullPath.Substring($resolvedToolsRoot.Length + 1)
    $parts = $relativeToTools -split "[\\/]"
    if ($parts.Count -gt 1) {
        if ($allowedToolsTopLevelFolders -notcontains $parts[0]) {
            Add-Violation "$relativePath is outside CeoAgent.Tools Abstractions/Implementation/Models folders"
        }
    }
    elseif ($allowedToolsRootFiles -notcontains $parts[0]) {
        Add-Violation "$relativePath is not an allowed CeoAgent.Tools root file"
    }
}

$namespaceRoots = @(
    @{ Path = "CeoAgent.Application\Company"; Namespace = "CeoAgent.Application.Company" },
    @{ Path = "CeoAgent.Infrastructure\Entities\Filters"; Namespace = "CeoAgent.Infrastructure.Entities.Filters" },
    @{ Path = "CeoAgent.Tools"; Namespace = "CeoAgent.Tools" }
)
$enforcedFolders = @("Abstractions", "Implementation", "Models")
foreach ($namespaceRoot in $namespaceRoots) {
    $absoluteRoot = Join-Path $repoRoot $namespaceRoot.Path
    $resolvedRoot = (Resolve-Path -LiteralPath $absoluteRoot).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch [regex]::Escape("\bin\") -and $_.FullName -notmatch [regex]::Escape("\obj\") }) {
        $relativePath = Get-RepoRelativePath -Path $file.FullName
        $fullPath = (Resolve-Path -LiteralPath $file.FullName).Path
        $relativeToRoot = $fullPath.Substring($resolvedRoot.Length + 1)
        $parts = $relativeToRoot -split "[\\/]"
        if ($parts.Count -lt 2 -or $enforcedFolders -notcontains $parts[0]) {
            continue
        }

        $namespaceParts = @()
        for ($index = 0; $index -lt ($parts.Count - 1); $index++) {
            $namespaceParts += $parts[$index]
        }

        $expectedNamespace = "$($namespaceRoot.Namespace).$($namespaceParts -join '.')"
        $content = Get-Content -LiteralPath $file.FullName -Raw
        $namespaceMatch = [regex]::Match($content, "namespace\s+(?<namespace>[A-Za-z0-9_.]+)\s*;")
        $actualNamespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups["namespace"].Value } else { "<missing>" }
        if ($actualNamespace -ne $expectedNamespace) {
            Add-Violation "$relativePath declares $actualNamespace; expected $expectedNamespace"
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Architecture check failed:" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host " - $violation" -ForegroundColor Red
    }

    exit 1
}

Write-Host "Architecture check passed."
