param(
    [string] $PostgresBin = (Join-Path $env:TEMP 'salekhpos-postgresql-18/pgsql/bin'),
    [int] $Port = 55438,
    [switch] $RunDotnetTests,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskRun = Join-Path $env:TEMP ('salekhpos-pg-test-' + [guid]::NewGuid().ToString('N'))
$taskData = Join-Path $taskRun 'data'
$taskPasswordFile = Join-Path $taskRun 'password.txt'
$taskOldPassword = $env:PGPASSWORD
$taskOldAdmin = $env:SALEKHPOS_TEST_ADMIN_CONNECTION
$taskOldRuntime = $env:SALEKHPOS_TEST_RUNTIME_CONNECTION
$taskStartAttempted = $false

function Invoke-PgTool([string] $Name, [string[]] $ToolArguments) {
    & (Join-Path $PostgresBin ($Name + '.exe')) @ToolArguments
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Path $taskRun | Out-Null
try {
    $env:PGPASSWORD = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Set-Content -LiteralPath $taskPasswordFile -Value $env:PGPASSWORD -Encoding ascii
    Invoke-PgTool 'initdb' @('-D', $taskData, '-U', 'postgres', '--encoding=UTF8', '--locale=C', '--auth=scram-sha-256', "--pwfile=$taskPasswordFile")
    Remove-Item -LiteralPath $taskPasswordFile
    $taskStartAttempted = $true
    Invoke-PgTool 'pg_ctl' @('-D', $taskData, '-l', (Join-Path $taskRun 'postgres.log'), '-o', "-h 127.0.0.1 -p $Port", '-w', 'start')

    $taskConnection = @('-X', '-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-v', 'ON_ERROR_STOP=1')
    $taskRuntimePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    "CREATE ROLE salekhpos_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS PASSWORD '$taskRuntimePassword';" | & (Join-Path $PostgresBin 'psql.exe') @taskConnection
    if ($LASTEXITCODE -ne 0) { throw 'Runtime role provisioning failed.' }
    $taskMigrations = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'infra/postgres/migrations') -Filter '*.sql' | Sort-Object Name)
    $taskTests = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'infra/postgres/tests') -Filter '*.sql' | Sort-Object Name)
    foreach ($taskMigration in $taskMigrations) {
        $taskVersion = $taskMigration.Name.Substring(0, 3)
        $taskMatchingTests = @($taskTests | Where-Object { $_.Name.StartsWith($taskVersion + '_') })
        if ($taskMatchingTests.Count -eq 0) { throw "Missing SQL tests for migration $taskVersion" }
        foreach ($taskTest in $taskMatchingTests | Where-Object { $_.Name.EndsWith('_before.sql') }) {
            Invoke-PgTool 'psql' ($taskConnection + @('-f', $taskTest.FullName))
        }
        Invoke-PgTool 'psql' ($taskConnection + @('-f', $taskMigration.FullName))
        foreach ($taskTest in $taskMatchingTests | Where-Object { -not $_.Name.EndsWith('_before.sql') }) {
            Invoke-PgTool 'psql' ($taskConnection + @('-f', $taskTest.FullName))
        }
    }
    if ($RunDotnetTests) {
        $taskBackup = Join-Path $taskRun 'restore-drill.dump'
        Invoke-PgTool 'pg_dump' @('-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'postgres', '-Fc', '-f', $taskBackup)
        Invoke-PgTool 'createdb' @('-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', 'salekhpos_restored')
        Invoke-PgTool 'pg_restore' @('-h', '127.0.0.1', '-p', "$Port", '-U', 'postgres', '-d', 'salekhpos_restored', '--exit-on-error', $taskBackup)
        Write-Output 'Logical backup restored into a separate disposable database; application tests run against that restored database.'
        $taskPrefix = "Host=127.0.0.1;Port=$Port;Database=salekhpos_restored;Timeout=5;Command Timeout=15;SSL Mode=Disable;"
        $env:SALEKHPOS_TEST_ADMIN_CONNECTION = $taskPrefix + "Username=postgres;Password=$env:PGPASSWORD"
        $env:SALEKHPOS_TEST_RUNTIME_CONNECTION = $taskPrefix + "Username=salekhpos_runtime;Password=$taskRuntimePassword"
        & (Join-Path $PSScriptRoot 'dotnet.ps1') test SalekhPos.slnx --configuration $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw '.NET tests failed against disposable PostgreSQL.' }
    }
    Write-Output 'PASS: PostgreSQL migration and isolation checks completed.'
}
finally {
    try {
        if ($taskStartAttempted) {
            & (Join-Path $PostgresBin 'pg_ctl.exe') -D $taskData -m fast -w stop
            if ($LASTEXITCODE -ne 0) { Write-Warning "Disposable test server cleanup needs review: $taskData" }
        }
    }
    finally {
        $env:PGPASSWORD = $taskOldPassword
        $env:SALEKHPOS_TEST_ADMIN_CONNECTION = $taskOldAdmin
        $env:SALEKHPOS_TEST_RUNTIME_CONNECTION = $taskOldRuntime
        if (Test-Path -LiteralPath $taskPasswordFile) { Remove-Item -LiteralPath $taskPasswordFile }
    }
    Write-Output "Disposable test data and logs: $taskRun"
}
