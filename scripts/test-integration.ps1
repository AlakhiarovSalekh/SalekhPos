#requires -Version 7.2
param([ValidateSet('Native','Docker')] [string] $PostgresMode = $(if ($IsWindows) { 'Native' } else { 'Docker' }))
$ErrorActionPreference = 'Stop'
# Includes migration regressions and a disposable logical restore before .NET integration tests.
if ($PostgresMode -eq 'Native') {
    & (Join-Path $PSScriptRoot 'test-postgres.ps1') -RunDotnetTests
} else {
    & (Join-Path $PSScriptRoot 'ci/test-postgres.ps1')
}
