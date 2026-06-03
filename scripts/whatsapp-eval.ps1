[CmdletBinding()]
param(
    [string]$EvalDirectory = "evals/whatsapp"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$evalRoot = Join-Path $repoRoot $EvalDirectory
$violations = [System.Collections.Generic.List[string]]::new()

function Add-Violation {
    param([string]$Message)
    $violations.Add($Message)
}

if (-not (Test-Path -LiteralPath $evalRoot)) {
    Add-Violation "Missing eval directory $EvalDirectory"
}
else {
    $files = Get-ChildItem -LiteralPath $evalRoot -File -Filter "*.json"
    if ($files.Count -eq 0) {
        Add-Violation "No WhatsApp eval fixture JSON files found."
    }

    foreach ($file in $files) {
        try {
            $fixture = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        }
        catch {
            Add-Violation "$($file.Name) is not valid JSON: $($_.Exception.Message)"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($fixture.id)) { Add-Violation "$($file.Name) missing id" }
        if ($fixture.version -lt 1) { Add-Violation "$($file.Name) version must be >= 1" }
        if ([string]::IsNullOrWhiteSpace($fixture.description)) { Add-Violation "$($file.Name) missing description" }
        if ($fixture.channel -ne "whatsapp_cloud") { Add-Violation "$($file.Name) channel must be whatsapp_cloud" }
        if ([string]::IsNullOrWhiteSpace($fixture.input.provider_channel_id)) { Add-Violation "$($file.Name) missing input.provider_channel_id" }
        if ([string]::IsNullOrWhiteSpace($fixture.input.customer_external_id)) { Add-Violation "$($file.Name) missing input.customer_external_id" }
        if ([string]::IsNullOrWhiteSpace($fixture.input.message_type)) { Add-Violation "$($file.Name) missing input.message_type" }
        if ([string]::IsNullOrWhiteSpace($fixture.input.message_text)) { Add-Violation "$($file.Name) missing input.message_text" }
        if ($fixture.expected.company_resolution -ne "by_provider_channel_id") { Add-Violation "$($file.Name) must resolve company by provider channel ID" }
        if ($fixture.expected.required_invariants.Count -eq 0) { Add-Violation "$($file.Name) missing required invariants" }
        if ($fixture.expected.acceptable_outcomes.Count -eq 0) { Add-Violation "$($file.Name) missing acceptable outcomes" }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "WhatsApp eval validation failed:" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host " - $violation" -ForegroundColor Red
    }

    exit 1
}

Write-Host "WhatsApp eval validation passed."
