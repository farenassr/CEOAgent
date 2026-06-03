[CmdletBinding()]
param(
    [string]$ApiHealthUrl = "http://localhost:5481/health",
    [string]$AppHostProject = "CeoAgent.AppHost/CeoAgent.AppHost.csproj",
    [switch]$StartAppHost,
    [int]$StartupSeconds = 35,
    [string]$LogPath = "TestResults/aspire-smoke-apphost.log"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $process = $null
    if ($StartAppHost) {
        $resolvedLogPath = Join-Path $repoRoot $LogPath
        $logDirectory = Split-Path -Parent $resolvedLogPath
        if (-not (Test-Path -LiteralPath $logDirectory)) {
            New-Item -ItemType Directory -Path $logDirectory | Out-Null
        }

        $process = Start-Process dotnet -ArgumentList @("run", "--project", $AppHostProject, "--launch-profile", "http") -PassThru -WindowStyle Hidden -RedirectStandardOutput $resolvedLogPath -RedirectStandardError $resolvedLogPath
        Start-Sleep -Seconds $StartupSeconds
    }

    try {
        $response = Invoke-WebRequest -Uri $ApiHealthUrl -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -ne 200) {
            throw "Health check returned status $($response.StatusCode)."
        }

        Write-Host "Health check passed: $ApiHealthUrl"
        if ($StartAppHost) {
            Write-Host "AppHost log captured at $LogPath"
            Write-Host "Use Aspire dashboard logs, traces, and metrics for deeper local observability checks."
        }
    }
    finally {
        if ($process -ne $null -and -not $process.HasExited) {
            Stop-Process -Id $process.Id
        }
    }
}
finally {
    Pop-Location
}
