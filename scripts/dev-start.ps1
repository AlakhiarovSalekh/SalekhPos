#requires -Version 7.2
param([ValidateSet('Backend','Web')] [string] $App = 'Backend')
$ErrorActionPreference = 'Stop'
if ($App -eq 'Web') { & pnpm --filter @salekhpos/web dev }
else { & (Join-Path $PSScriptRoot 'dotnet.ps1') run --project backend/src/Bootstrapper/SalekhPos.Api }
if ($LASTEXITCODE -ne 0) { throw "Development server failed with exit code $LASTEXITCODE." }
