[CmdletBinding()]
param(
    [string]$Solution = "CEOAgent.slnx",
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    if ($Fix) {
        dotnet format $Solution
    }
    else {
        dotnet format $Solution --verify-no-changes
    }
}
finally {
    Pop-Location
}
