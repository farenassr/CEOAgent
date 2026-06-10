[CmdletBinding()]
param(
    [switch]$IncludeFormat,
    [switch]$IncludeBuild,
    [switch]$IncludeTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    ./AIHarness/scripts/doc-gardening.ps1
    ./AIHarness/scripts/whatsapp-eval.ps1
    ./AIHarness/scripts/whatsapp-behavior-eval.ps1
    ./AIHarness/scripts/architecture-check.ps1

    if ($IncludeFormat) {
        ./AIHarness/scripts/format.ps1
    }

    if ($IncludeBuild) {
        ./AIHarness/scripts/build.ps1
    }

    if ($IncludeTests) {
        dotnet test tests/CeoAgent.IntegrationTests/CeoAgent.IntegrationTests.csproj --no-build
    }
}
finally {
    Pop-Location
}
