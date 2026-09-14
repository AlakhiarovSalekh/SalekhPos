#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') build SalekhPos.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/packages-api-client build
if ($LASTEXITCODE -ne 0) { throw 'Shared API client build failed.' }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/apps-mobile typecheck
if ($LASTEXITCODE -ne 0) { throw 'Mobile typecheck failed.' }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/web build
if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
