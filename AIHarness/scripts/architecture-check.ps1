[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceRoot = Join-Path $repoRoot "src"
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

    $absoluteProjectDirectory = Join-Path $repoRoot $ProjectDirectory
    if (-not (Test-Path -LiteralPath $absoluteProjectDirectory)) {
        $srcProjectDirectory = Join-Path $sourceRoot $ProjectDirectory
        if (Test-Path -LiteralPath $srcProjectDirectory) {
            $absoluteProjectDirectory = $srcProjectDirectory
        }
    }

    foreach ($pattern in $Patterns) {
        Get-ChildItem -LiteralPath $absoluteProjectDirectory -Recurse -File -Filter $pattern |
            Where-Object {
                $_.FullName -notmatch [regex]::Escape("\bin\") -and
                $_.FullName -notmatch [regex]::Escape("\obj\")
            }
    }
}

$productionProjects = @(
    "CeoAgent.ApiService",
    "CeoAgent.AppHost",
    "CeoAgent.Application",
    "CeoAgent.Infrastructure",
    "CeoAgent.ServiceDefaults",
    "CeoAgent.Shared",
    "CeoAgent.Worker"
)

$allowedProjectReferences = @{
    "CeoAgent.ApiService" = @("CeoAgent.Application", "CeoAgent.Infrastructure", "CeoAgent.ServiceDefaults", "CeoAgent.Shared")
    "CeoAgent.AppHost" = @("CeoAgent.ApiService", "CeoAgent.Worker")
    "CeoAgent.Application" = @("CeoAgent.Shared")
    "CeoAgent.Infrastructure" = @("CeoAgent.Application", "CeoAgent.Shared")
    "CeoAgent.ServiceDefaults" = @()
    "CeoAgent.Shared" = @()
    "CeoAgent.Worker" = @("CeoAgent.Application", "CeoAgent.Infrastructure", "CeoAgent.ServiceDefaults", "CeoAgent.Shared")
}

foreach ($projectDirectory in $productionProjects) {
    $projectFile = Get-ChildItem -LiteralPath (Join-Path $sourceRoot $projectDirectory) -File -Filter "*.csproj" | Select-Object -First 1
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

$providerMarkers = @("Azure.Security.KeyVault.Secrets", "Google.Apis", "OpenAI.Responses", "Microsoft.Agents.AI.OpenAI", "Refit")
foreach ($projectDirectory in $productionProjects) {
    foreach ($file in Get-ProductionFile -ProjectDirectory $projectDirectory -Patterns @("*.cs", "*.csproj")) {
        $relativePath = Get-RepoRelativePath -Path $file.FullName
        $isInfrastructureImplementation = $relativePath.StartsWith("src$([System.IO.Path]::DirectorySeparatorChar)CeoAgent.Infrastructure$([System.IO.Path]::DirectorySeparatorChar)Implementation$([System.IO.Path]::DirectorySeparatorChar)")
        $isInfrastructureApiClient = $relativePath.StartsWith("src$([System.IO.Path]::DirectorySeparatorChar)CeoAgent.Infrastructure$([System.IO.Path]::DirectorySeparatorChar)ApiClient$([System.IO.Path]::DirectorySeparatorChar)")
        $isInfrastructureProjectFile = $relativePath -eq "src$([System.IO.Path]::DirectorySeparatorChar)CeoAgent.Infrastructure$([System.IO.Path]::DirectorySeparatorChar)CEOAgent.Infrastructure.csproj"
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($marker in $providerMarkers) {
            if (-not $isInfrastructureImplementation -and -not $isInfrastructureApiClient -and -not $isInfrastructureProjectFile -and $content.Contains($marker)) {
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

$implementationRoot = Join-Path $sourceRoot "CeoAgent.Infrastructure\Implementation"
$resolvedImplementationRoot = (Resolve-Path -LiteralPath $implementationRoot).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$allowedImplementationTopLevelFolders = @("AI", "AITools", "Company", "GoogleCalendar", "Messaging", "OpenAI", "Secrets")
foreach ($file in Get-ChildItem -LiteralPath $implementationRoot -Recurse -File -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch [regex]::Escape("\bin\") -and $_.FullName -notmatch [regex]::Escape("\obj\") }) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $fullPath = (Resolve-Path -LiteralPath $file.FullName).Path
    $relativeToImplementation = $fullPath.Substring($resolvedImplementationRoot.Length + 1)
    $parts = $relativeToImplementation -split "[\\/]"
    if ($parts.Count -lt 2 -or $allowedImplementationTopLevelFolders -notcontains $parts[0]) {
        Add-Violation "$relativePath is outside approved CeoAgent.Infrastructure Implementation folders"
    }
}

$namespaceRoots = @(
    @{ Path = "src\CeoAgent.Application\Abstractions"; Namespace = "CeoAgent.Application.Abstractions" },
    @{ Path = "src\CeoAgent.Shared\AI"; Namespace = "CeoAgent.Shared.AI" },
    @{ Path = "src\CeoAgent.Shared\AITools"; Namespace = "CeoAgent.Shared.AITools" },
    @{ Path = "src\CeoAgent.Shared\Calendar"; Namespace = "CeoAgent.Shared.Calendar" },
    @{ Path = "src\CeoAgent.Shared\Jobs"; Namespace = "CeoAgent.Shared.Jobs" },
    @{ Path = "src\CeoAgent.Shared\Messaging"; Namespace = "CeoAgent.Shared.Messaging" },
    @{ Path = "src\CeoAgent.Infrastructure\Implementation"; Namespace = "CeoAgent.Infrastructure.Implementation" },
    @{ Path = "src\CeoAgent.Infrastructure\Entities\Filters"; Namespace = "CeoAgent.Infrastructure.Entities.Filters" }
)
$enforcedFolders = @("Abstractions", "Implementation", "Implementations", "Models")
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
