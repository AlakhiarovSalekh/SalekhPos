#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') build SalekhPos.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
