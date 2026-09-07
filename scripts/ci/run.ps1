#requires -Version 7.2
param(
    [ValidateSet('Docker', 'Native')] [string] $PostgresMode = 'Docker',
    [string] $ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-quality-reports')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskDotnet = Join-Path $taskRoot 'scripts/dotnet.ps1'
$taskSolution = Join-Path $taskRoot 'SalekhPos.sln'
$taskPreviousCi = $env:ContinuousIntegrationBuild
$env:ContinuousIntegrationBuild = 'true'

function Invoke-DotnetCheck([string[]] $Arguments) {
    & $taskDotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}

try {
    & (Join-Path $PSScriptRoot 'check-requirements.ps1')
    & (Join-Path $PSScriptRoot 'check-structure.ps1') -RequireTracked
    & (Join-Path $PSScriptRoot 'check-architecture.ps1')
    & (Join-Path $PSScriptRoot 'check-secrets.ps1') -ReportDirectory $ReportDirectory
    Invoke-DotnetCheck @('restore', $taskSolution, '--locked-mode', '-p:NuGetAudit=true', '-p:NuGetAuditMode=all', '-p:NuGetAuditLevel=low')
    Invoke-DotnetCheck @('build', $taskSolution, '--configuration', 'Release', '--no-restore', '-warnaserror')
    Invoke-DotnetCheck @('format', $taskSolution, '--verify-no-changes', '--no-restore', '--severity', 'info')
    & (Join-Path $PSScriptRoot 'check-dependencies.ps1') -ReportDirectory $ReportDirectory
    if ($PostgresMode -eq 'Docker') {
        & (Join-Path $PSScriptRoot 'test-postgres.ps1') -ReportDirectory $ReportDirectory
    }
    else {
        & (Join-Path $taskRoot 'scripts/test-postgres.ps1') -RunDotnetTests
        if ($LASTEXITCODE -ne 0) { throw 'Native PostgreSQL/integration checks failed.' }
    }
    Write-Output 'PASS: complete local CI gate finished (architecture, secrets, restore, build, format, dependencies, PostgreSQL and .NET tests).'
}
finally { $env:ContinuousIntegrationBuild = $taskPreviousCi }
