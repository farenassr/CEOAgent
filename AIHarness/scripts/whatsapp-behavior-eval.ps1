[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    ./AIHarness/scripts/whatsapp-eval.ps1
    ./AIHarness/scripts/test.ps1 -TreeNodeFilter "/*/*/*WhatsApp*/*|/*/*/*Webhook*/*|/*/*/*AdminWhatsApp*/*"
}
finally {
    Pop-Location
}
