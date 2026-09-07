#requires -Version 7.2
param([ValidateSet('Native','Docker')] [string] $PostgresMode = $(if ($IsWindows) { 'Native' } else { 'Docker' }))
& (Join-Path $PSScriptRoot 'ci/run.ps1') -PostgresMode $PostgresMode
