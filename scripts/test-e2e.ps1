#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test-postgres.ps1') -RunWebIdentityTests
