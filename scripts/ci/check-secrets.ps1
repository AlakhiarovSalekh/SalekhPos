#requires -Version 7.2
param([string] $ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-quality-reports'))

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskVersion = '8.30.1'
$taskArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
if ($taskArchitecture -ne 'X64') { throw 'The pinned Gitleaks installer currently supports x64 Windows/Linux only.' }
if ($IsWindows) {
    $taskArchiveName = "gitleaks_${taskVersion}_windows_x64.zip"
    $taskChecksum = 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'
    $taskExecutableName = 'gitleaks.exe'
}
elseif ($IsLinux) {
    $taskArchiveName = "gitleaks_${taskVersion}_linux_x64.tar.gz"
    $taskChecksum = '551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb'
    $taskExecutableName = 'gitleaks'
}
else { throw 'Use x64 Windows or Linux for the pinned local secret scan.' }
$taskToolDirectory = Join-Path ([IO.Path]::GetTempPath()) "salekhpos-tools/gitleaks/$taskVersion"
New-Item -ItemType Directory -Path $taskToolDirectory, $ReportDirectory -Force | Out-Null
$taskArchive = Join-Path $taskToolDirectory $taskArchiveName
if (-not (Test-Path -LiteralPath $taskArchive)) {
    Invoke-WebRequest -Uri "https://github.com/gitleaks/gitleaks/releases/download/v$taskVersion/$taskArchiveName" -OutFile $taskArchive
}
if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskChecksum) {
    throw 'Gitleaks release archive checksum mismatch. The scanner was not executed.'
}
# Re-extract from the verified archive so a modified cached executable is not trusted.
if ($IsWindows) { Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskToolDirectory -Force }
else {
    & tar -xzf $taskArchive -C $taskToolDirectory $taskExecutableName
    if ($LASTEXITCODE -ne 0) { throw 'Could not extract the verified Gitleaks archive.' }
}
$taskExecutable = Join-Path $taskToolDirectory $taskExecutableName
& $taskExecutable dir $taskRoot --redact=100 --no-banner --report-format json --report-path (Join-Path $ReportDirectory 'secrets-worktree.json')
if ($LASTEXITCODE -ne 0) { throw 'Working tree secret scan failed. Review the redacted scanner output.' }
& git -C $taskRoot rev-parse --verify HEAD 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    & $taskExecutable git $taskRoot --log-opts=--all --redact=100 --no-banner --report-format json --report-path (Join-Path $ReportDirectory 'secrets-history.json')
    if ($LASTEXITCODE -ne 0) { throw 'Git history secret scan failed. Review the redacted scanner output.' }
}
elseif ($env:GITHUB_ACTIONS -eq 'true') { throw 'CI must scan a checked-out commit and its Git history.' }
else { Write-Output 'Git history scan is not applicable: this local repository has no commits yet.' }
Write-Output 'PASS: Gitleaks working tree scan and available Git history scan completed.'
