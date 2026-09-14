#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') format SalekhPos.sln --verify-no-changes --no-restore --severity info
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/packages-api-client typecheck
if ($LASTEXITCODE -ne 0) { throw 'Shared API client typecheck failed.' }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/apps-mobile typecheck
if ($LASTEXITCODE -ne 0) { throw 'Mobile typecheck failed.' }
& (Join-Path $PSScriptRoot 'pnpm.ps1') --filter @salekhpos/web lint
if ($LASTEXITCODE -ne 0) { throw 'Web lint failed.' }
