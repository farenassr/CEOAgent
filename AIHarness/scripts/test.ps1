[CmdletBinding()]
param(
    [string]$Solution = "CEOAgent.slnx",
    [string]$Configuration = "Debug",
    [string]$Filter = "",
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

    if ($Filter.Length -gt 0) {
        $arguments += @("--filter", $Filter)
    }

    dotnet @arguments
}
finally {
    Pop-Location
}
