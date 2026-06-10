[CmdletBinding()]
param(
    [string]$Solution = "CEOAgent.slnx",
    [string]$Configuration = "Debug",
    [Alias("Filter")]
    [string]$TreeNodeFilter = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    $arguments = @("test", $Solution, "--configuration", $Configuration)

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    if ($TreeNodeFilter.Length -gt 0) {
        $arguments += @("--treenode-filter", $TreeNodeFilter)
    }

    dotnet @arguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
