#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') run --project backend/src/Bootstrapper/SalekhPos.Api
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
