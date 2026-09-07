#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') test backend/tests/Unit/SalekhPos.Tests.csproj --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
