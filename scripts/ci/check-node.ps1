#requires -Version 7.2
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskPnpm = Join-Path $taskRoot 'scripts/pnpm.ps1'
$taskPreviousCi = $env:CI
$env:CI = 'true'

function Invoke-Pnpm([string[]] $Arguments) {
    & $taskPnpm @Arguments
    if ($LASTEXITCODE -ne 0) { throw "pnpm $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}

try {
    Invoke-Pnpm @('install', '--frozen-lockfile')
    Invoke-Pnpm @('--filter', '@salekhpos/packages-api-client', 'typecheck')
    Invoke-Pnpm @('--filter', '@salekhpos/packages-api-client', 'test')
    Invoke-Pnpm @('--filter', '@salekhpos/apps-mobile', 'typecheck')
    Invoke-Pnpm @('--filter', '@salekhpos/apps-mobile', 'test')
    Invoke-Pnpm @('--filter', '@salekhpos/apps-mobile', 'exec', 'expo', 'install', '--check')
    Invoke-Pnpm @('--filter', '@salekhpos/web', 'typecheck')
    Invoke-Pnpm @('--filter', '@salekhpos/web', 'lint')
    Invoke-Pnpm @('--filter', '@salekhpos/web', 'build')
    Write-Output 'PASS: Node workspace install, shared API client, mobile and web gates completed.'
}
finally { $env:CI = $taskPreviousCi }
