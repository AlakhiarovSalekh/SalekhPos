#requires -Version 7.2
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'dotnet.ps1') restore (Join-Path $PSScriptRoot '../SalekhPos.sln') --configfile (Join-Path $PSScriptRoot '../NuGet.Config') --locked-mode
if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE." }
& pnpm install --frozen-lockfile --ignore-scripts
if ($LASTEXITCODE -ne 0) { throw 'Web dependency installation failed.' }
