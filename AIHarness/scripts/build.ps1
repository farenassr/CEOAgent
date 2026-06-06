[CmdletBinding()]
param(
    [string]$Solution = "CEOAgent.slnx",
    [string]$Configuration = "Debug",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    if (-not $NoRestore) {
        dotnet restore $Solution
    }

    $arguments = @("build", $Solution, "--configuration", $Configuration)
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    dotnet @arguments
}
finally {
    Pop-Location
}
