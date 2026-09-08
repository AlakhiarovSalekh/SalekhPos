#requires -Version 7.2
param(
    [string] $KeycloakDirectory = (Join-Path $env:TEMP 'salekhpos-identity-provider/keycloak-26.7.3'),
    [string] $JavaDirectory = (Join-Path $env:TEMP 'salekhpos-java25/jdk-25.0.4.1+1')
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This native identity browser runner currently requires Windows.' }
if (-not $env:SALEKHPOS_TEST_RUNTIME_CONNECTION) { throw 'Run through scripts/test-postgres.ps1 -RunWebIdentityTests.' }
$taskJavaExecutable = Join-Path $JavaDirectory 'bin/java.exe'
$taskKeycloakExecutable = Join-Path $KeycloakDirectory 'bin/kc.bat'
if (-not (Test-Path -LiteralPath $taskJavaExecutable)) { throw "Java runtime not found: $taskJavaExecutable" }
if (-not (Test-Path -LiteralPath $taskKeycloakExecutable)) { throw "Keycloak runtime not found: $taskKeycloakExecutable" }
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskRun = Join-Path $env:TEMP ('salekhpos-browser-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskRun) | Out-Null
$taskRealm = 'salekhpos-' + [guid]::NewGuid().ToString('N')
$taskProcesses = [Collections.Generic.List[Diagnostics.Process]]::new()
$taskCopies = [Collections.Generic.List[object]]::new()
$taskEnvironment = @{}
function Set-TestEnvironment([string] $Name, [string] $Value) {
    if (-not $taskEnvironment.ContainsKey($Name)) { $taskEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name) }
    [Environment]::SetEnvironmentVariable($Name, $Value)
}
function Start-TestProcess([string] $File, [string[]] $Arguments, [string] $Directory, [string] $Name) {
    # Stream output with .NET async I/O; PowerShell redirection can block its
    # pipeline for a long-lived child even without Start-Process -Wait.
    $taskInfo = [Diagnostics.ProcessStartInfo]::new()
    $taskInfo.FileName = $File
    $taskInfo.WorkingDirectory = $Directory
    $taskInfo.UseShellExecute = $false
    $taskInfo.CreateNoWindow = $true
    $taskInfo.RedirectStandardOutput = $true
    $taskInfo.RedirectStandardError = $true
    if ($File.EndsWith('.bat')) {
        $taskInfo.FileName = $env:ComSpec
        $taskInfo.Arguments = '/d /s /c ""' + $File + '" ' + ($Arguments -join ' ') + '"'
    } else {
        foreach ($taskArgument in $Arguments) { $taskInfo.ArgumentList.Add($taskArgument) }
    }
    $taskProcess = [Diagnostics.Process]::Start($taskInfo)
    $taskProcesses.Add($taskProcess)
    foreach ($taskOutput in @(@{ Reader=$taskProcess.StandardOutput; Suffix='stdout' }, @{ Reader=$taskProcess.StandardError; Suffix='stderr' })) {
        $taskStream = [IO.File]::Open((Join-Path $taskRun ($Name + '.' + $taskOutput.Suffix + '.log')), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $taskCopies.Add(@{ Stream=$taskStream; Task=$taskOutput.Reader.BaseStream.CopyToAsync($taskStream) })
    }
    Write-Output "Started local $Name process."
}
function Wait-Ready([string] $Url) {
    for ($taskAttempt = 0; $taskAttempt -lt 180; $taskAttempt++) {
        try { $null = Invoke-WebRequest $Url -TimeoutSec 10; return } catch { Start-Sleep -Seconds 1 }
    }
    throw "Local service did not become ready: $Url. Inspect logs in $taskRun."
}
$taskImport = Join-Path $KeycloakDirectory "data/import/$taskRealm-realm.json"
try {
    foreach ($taskPort in @(3000,5080,8180)) {
        if (Get-NetTCPConnection -LocalPort $taskPort -State Listen -ErrorAction SilentlyContinue) { throw "Local port $taskPort is already occupied." }
    }
    $taskSecret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $taskPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $taskRealmConfig = @{
        realm=$taskRealm; enabled=$true; sslRequired='none'; registrationAllowed=$false; bruteForceProtected=$true
        loginWithEmailAllowed=$false
        clients=@(@{clientId='salekhpos-web'; enabled=$true; protocol='openid-connect'; publicClient=$false; secret=$taskSecret
            standardFlowEnabled=$true; implicitFlowEnabled=$false; directAccessGrantsEnabled=$false; serviceAccountsEnabled=$false
            redirectUris=@('http://localhost:3000/auth/callback'); webOrigins=@('http://localhost:3000')
            attributes=@{'pkce.code.challenge.method'='S256'; 'post.logout.redirect.uris'='http://localhost:3000/auth/signed-out'} })
        users=@(@{username='browser-user'; enabled=$true; email='browser@example.test'; emailVerified=$true; firstName='Store'; lastName='Operator'
            credentials=@(@{type='password'; value=$taskPassword; temporary=$false})})
    }
    [IO.Directory]::CreateDirectory((Split-Path $taskImport -Parent)) | Out-Null
    $taskRealmConfig | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $taskImport
    Set-TestEnvironment 'JAVA_HOME' $JavaDirectory
    Start-TestProcess $taskKeycloakExecutable @('start-dev','--http-host=127.0.0.1','--http-port=8180','--import-realm') $KeycloakDirectory 'provider'
    Wait-Ready "http://127.0.0.1:8180/realms/$taskRealm/.well-known/openid-configuration"
    Remove-Item -LiteralPath $taskImport
    Set-TestEnvironment 'ASPNETCORE_ENVIRONMENT' 'Development'
    Set-TestEnvironment 'ASPNETCORE_URLS' 'http://127.0.0.1:5080'
    Set-TestEnvironment 'ConnectionStrings__Application' $env:SALEKHPOS_TEST_RUNTIME_CONNECTION
    Set-TestEnvironment 'WebAuthentication__Authority' "http://127.0.0.1:8180/realms/$taskRealm"
    Set-TestEnvironment 'WebAuthentication__ClientId' 'salekhpos-web'
    Set-TestEnvironment 'WebAuthentication__ClientSecret' $taskSecret
    Set-TestEnvironment 'WebAuthentication__PublicOrigin' 'http://localhost:3000'
    Set-TestEnvironment 'WebAuthentication__AllowLoopbackHttp' 'true'
    Set-TestEnvironment 'NEXT_TELEMETRY_DISABLED' '1'
    Set-TestEnvironment 'SALEKHPOS_BROWSER_PASSWORD' $taskPassword
    Start-TestProcess (Join-Path $env:TEMP 'salekhpos-dotnet/dotnet.exe') @('run','--project','backend/src/Bootstrapper/SalekhPos.Api','--configuration','Release','--no-build','--no-restore') $taskRoot 'api'
    Wait-Ready 'http://127.0.0.1:5080/health/live'
    $taskWeb = Join-Path $taskRoot 'apps/web'
    Start-TestProcess (Get-Command node).Source @('node_modules/next/dist/bin/next','start','--hostname','127.0.0.1') $taskWeb 'web'
    Wait-Ready 'http://127.0.0.1:3000'
    & npx --yes agent-browser@0.36.0 open http://localhost:3000
    if ($LASTEXITCODE -ne 0) { throw 'Browser could not open the web application.' }
    & npx --yes agent-browser@0.36.0 snapshot -i
    & npx --yes agent-browser@0.36.0 screenshot (Join-Path $taskRun 'home.png')
    & npx --yes agent-browser@0.36.0 close
    & pnpm --filter @salekhpos/web test:e2e
    if ($LASTEXITCODE -ne 0) { throw "Real provider browser tests failed. Logs: $taskRun" }
    Write-Output "PASS: real provider browser login/logout. Screenshot and logs: $taskRun"
}
finally {
    foreach ($taskProcess in $taskProcesses) {
        if (-not $taskProcess.HasExited) { & taskkill /PID $taskProcess.Id /T /F *> $null }
    }
    foreach ($taskCopy in $taskCopies) { try { $null = $taskCopy.Task.Wait(5000) } finally { $taskCopy.Stream.Dispose() } }
    foreach ($taskName in $taskEnvironment.Keys) { [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName]) }
    if (Test-Path -LiteralPath $taskImport) { Remove-Item -LiteralPath $taskImport }
}
