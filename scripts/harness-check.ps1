[CmdletBinding()]
param(
    [switch]$IncludeFormat,
    [switch]$IncludeBuild,
    [switch]$IncludeTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    ./scripts/doc-gardening.ps1
    ./scripts/whatsapp-eval.ps1
    ./scripts/architecture-check.ps1

    if ($IncludeFormat) {
        ./scripts/format.ps1
    }

    if ($IncludeBuild) {
        ./scripts/build.ps1
    }

    if ($IncludeTests) {
        dotnet test tests/CeoAgent.IntegrationTests/CeoAgent.IntegrationTests.csproj --no-build
    }
}
finally {
    Pop-Location
}
