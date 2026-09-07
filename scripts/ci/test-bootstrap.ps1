#requires -Version 7.2
param([string] $Configuration = 'Release')
# Called only by disposable PostgreSQL runners after schema restoration.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($env:SALEKHPOS_TEST_ADMIN_CONNECTION) -or
    [string]::IsNullOrWhiteSpace($env:SALEKHPOS_TEST_RUNTIME_CONNECTION)) { throw 'Disposable test connections are required.' }
$taskPreviousConnection = $env:SALEKHPOS_BOOTSTRAP_CONNECTION
$taskPreviousDevelopment = $env:SALEKHPOS_BOOTSTRAP_LOCAL_DEVELOPMENT
try {
    $env:SALEKHPOS_BOOTSTRAP_LOCAL_DEVELOPMENT = 'true'
    $env:SALEKHPOS_BOOTSTRAP_CONNECTION = $env:SALEKHPOS_TEST_ADMIN_CONNECTION
    $taskArguments = @('run', '--project', (Join-Path $taskRoot 'tools/cli/SalekhPos.Cli'),
        '--configuration', $Configuration, '--no-build', '--no-restore', '--', 'bootstrap-root',
        '51000000-0000-0000-0000-000000000001', 'https://identity.example.test', 'platform-root',
        'Integration fixture original root')
    foreach ($taskAttempt in 1..2) {
        & (Join-Path $taskRoot 'scripts/dotnet.ps1') @taskArguments
        if ($LASTEXITCODE -ne 0) { throw "Root bootstrap/retry failed: attempt $taskAttempt" }
    }
    $taskArguments[-4] = [guid]::NewGuid().ToString()
    $taskArguments[-2] = 'replacement-root'
    & (Join-Path $taskRoot 'scripts/dotnet.ps1') @taskArguments
    if ($LASTEXITCODE -ne 1) { throw 'Root replacement was not rejected.' }
    $env:SALEKHPOS_BOOTSTRAP_CONNECTION = $env:SALEKHPOS_TEST_RUNTIME_CONNECTION
    & (Join-Path $taskRoot 'scripts/dotnet.ps1') @taskArguments
    if ($LASTEXITCODE -ne 1) { throw 'Runtime bootstrap was not rejected.' }
    Write-Output 'PASS: operator bootstrap executable, retry, immutable root and runtime denial.'
}
finally {
    $env:SALEKHPOS_BOOTSTRAP_CONNECTION = $taskPreviousConnection
    $env:SALEKHPOS_BOOTSTRAP_LOCAL_DEVELOPMENT = $taskPreviousDevelopment
}
