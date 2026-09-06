#requires -Version 7.2
param([string] $ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-quality-reports'))

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# Official postgres 18.6 multi-platform digest, verified from Docker Hub on 2026-09-06.
$taskImage = 'postgres:18.6@sha256:4ef4dbc939d61acea57712655ddb4b4ab27419c913f94cca0cd57cb3ea3c2280'
$taskContainerName = 'salekhpos-ci-' + [guid]::NewGuid().ToString('N')
$taskContainerId = $null
$taskEnvironmentNames = @('POSTGRES_PASSWORD', 'SALEKHPOS_TEST_ADMIN_CONNECTION', 'SALEKHPOS_TEST_RUNTIME_CONNECTION')
$taskPreviousEnvironment = @{}
foreach ($taskName in $taskEnvironmentNames) {
    $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName)
}

function Invoke-DockerCheck([string[]] $Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}

$taskMigrations = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'infra/postgres/migrations') -Filter '*.sql' | Sort-Object Name)
$taskSqlTests = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'infra/postgres/tests') -Filter '*.sql' | Sort-Object Name)
if ($taskMigrations.Count -eq 0 -or $taskSqlTests.Count -eq 0) { throw 'Migrations and SQL tests must both exist.' }
$taskVersions = @{}
foreach ($taskMigration in $taskMigrations) {
    if ($taskMigration.Name -notmatch '^(\d{3})_[a-z0-9_]+\.sql$') { throw "Invalid migration filename: $($taskMigration.Name)" }
    $taskVersion = $Matches[1]
    if ($taskVersions.ContainsKey($taskVersion)) { throw "Duplicate migration version $taskVersion." }
    $taskVersions[$taskVersion] = $true
    if (@($taskSqlTests | Where-Object { $_.Name.StartsWith($taskVersion + '_') }).Count -eq 0) {
        throw "Migration $taskVersion has no SQL regression tests."
    }
}
foreach ($taskTest in $taskSqlTests) {
    if ($taskTest.Name -notmatch '^(\d{3})_[a-z0-9_]+\.sql$' -or -not $taskVersions.ContainsKey($Matches[1])) {
        throw "SQL test has no matching migration: $($taskTest.Name)"
    }
}

try {
    $env:POSTGRES_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $taskRuntimePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Output "::add-mask::$env:POSTGRES_PASSWORD"
        Write-Output "::add-mask::$taskRuntimePassword"
    }
    $taskContainerId = (Invoke-DockerCheck @('run', '--detach', '--rm', '--name', $taskContainerName,
        '--publish', '127.0.0.1::5432', '--env', 'POSTGRES_PASSWORD', '--env', 'POSTGRES_DB=salekhpos_test',
        '--env', 'POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256', $taskImage) | Select-Object -Last 1).Trim()
    if ($taskContainerId -notmatch '^[a-f0-9]{64}$') { throw 'Docker did not return a valid disposable container ID.' }
    $taskReady = $false
    for ($taskAttempt = 0; $taskAttempt -lt 60; $taskAttempt++) {
        & docker exec $taskContainerId pg_isready -h 127.0.0.1 -U postgres -d salekhpos_test *> $null
        if ($LASTEXITCODE -eq 0) { $taskReady = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $taskReady) { throw 'The disposable PostgreSQL container did not become ready within 60 seconds.' }
    $taskPortMapping = (Invoke-DockerCheck @('port', $taskContainerId, '5432/tcp') | Select-Object -First 1).Trim()
    if ($taskPortMapping -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'PostgreSQL must bind exclusively to loopback.' }
    $taskPort = $Matches[1]
    $taskPsql = @('exec', '-i', $taskContainerId, 'psql', '-X', '-U', 'postgres', '-d', 'salekhpos_test', '-v', 'ON_ERROR_STOP=1')
    # Password is generated hex, passed on stdin, and never included in process arguments.
    "CREATE ROLE salekhpos_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD '$taskRuntimePassword';" | & docker @taskPsql
    if ($LASTEXITCODE -ne 0) { throw 'Restricted PostgreSQL runtime role provisioning failed.' }
    Invoke-DockerCheck @('cp', (Join-Path $taskRoot 'infra/postgres'), "${taskContainerId}:/checks")
    foreach ($taskMigration in $taskMigrations) {
        $taskVersion = $taskMigration.Name.Substring(0, 3)
        Write-Output "Applying $($taskMigration.Name) and its regression checks."
        foreach ($taskTest in $taskSqlTests | Where-Object { $_.Name.StartsWith($taskVersion + '_') -and $_.Name.EndsWith('_before.sql') }) {
            Invoke-DockerCheck ($taskPsql + @('-f', ('/checks/tests/' + $taskTest.Name)))
        }
        Invoke-DockerCheck ($taskPsql + @('-f', ('/checks/migrations/' + $taskMigration.Name)))
        foreach ($taskTest in $taskSqlTests | Where-Object { $_.Name.StartsWith($taskVersion + '_') -and -not $_.Name.EndsWith('_before.sql') }) {
            Invoke-DockerCheck ($taskPsql + @('-f', ('/checks/tests/' + $taskTest.Name)))
        }
    }
    Invoke-DockerCheck @('exec', $taskContainerId, 'pg_dump', '-U', 'postgres', '-d', 'salekhpos_test', '-Fc', '-f', '/tmp/restore-drill.dump')
    Invoke-DockerCheck @('exec', $taskContainerId, 'createdb', '-U', 'postgres', 'salekhpos_restored')
    Invoke-DockerCheck @('exec', $taskContainerId, 'pg_restore', '-U', 'postgres', '-d', 'salekhpos_restored', '--exit-on-error', '/tmp/restore-drill.dump')
    Write-Output 'Logical backup restored; application integration tests target the restored database.'
    $taskConnectionPrefix = "Host=127.0.0.1;Port=$taskPort;Database=salekhpos_restored;Timeout=5;Command Timeout=15;Include Error Detail=false;"
    $env:SALEKHPOS_TEST_ADMIN_CONNECTION = $taskConnectionPrefix + "Username=postgres;Password=$env:POSTGRES_PASSWORD"
    $env:SALEKHPOS_TEST_RUNTIME_CONNECTION = $taskConnectionPrefix + "Username=salekhpos_runtime;Password=$taskRuntimePassword"
    New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
    & (Join-Path $taskRoot 'scripts/dotnet.ps1') test (Join-Path $taskRoot 'SalekhPos.slnx') --configuration Release --no-build --no-restore --logger trx --results-directory (Join-Path $ReportDirectory 'tests')
    if ($LASTEXITCODE -ne 0) { throw '.NET unit and live PostgreSQL integration tests failed.' }
    Write-Output "PASS: $($taskMigrations.Count) migrations, $($taskSqlTests.Count) SQL files, and the .NET solution tests completed."
}
finally {
    foreach ($taskName in $taskEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName])
    }
    if ($null -ne $taskContainerId -and $taskContainerId -match '^[a-f0-9]{64}$') {
        Invoke-DockerCheck @('rm', '--force', '--volumes', $taskContainerId) | Out-Null
    }
}
