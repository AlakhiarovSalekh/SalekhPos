#requires -Version 7.2
param([string] $RepositoryRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference = 'Stop'
$taskLegacy = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'infra/postgres/migrations') -Filter '*.sql')
$taskOwned = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'backend/src/Modules') -Filter '*.sql' -Recurse |
    Where-Object { $_.FullName.Replace('\','/') -match '/Persistence/Migrations/[^/]+\.sql$' })
$taskSeen = @{}
foreach ($taskMigration in @($taskLegacy + $taskOwned | Sort-Object Name)) {
    if ($taskMigration.Name -notmatch '^(\d{3})_[a-z0-9_]+\.sql$') { throw "Invalid migration name: $($taskMigration.Name)" }
    if ($taskSeen.ContainsKey($Matches[1])) { throw "Duplicate migration version: $($Matches[1])" }
    $taskSeen[$Matches[1]] = $true
    $taskMigration
}
