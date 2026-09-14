#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & corepack pnpm @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
