#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') format SalekhPos.sln --verify-no-changes --no-restore --severity info
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
