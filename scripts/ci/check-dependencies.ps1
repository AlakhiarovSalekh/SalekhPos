#requires -Version 7.2
param([string] $ReportDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-quality-reports'))

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
$taskReportPath = Join-Path $ReportDirectory 'dependencies.json'
# dotnet package list can exit zero even when it reports vulnerable packages.
$taskOutput = & (Join-Path $taskRoot 'scripts/dotnet.ps1') list (Join-Path $taskRoot 'SalekhPos.slnx') package --vulnerable --include-transitive --format json --output-version 1 --no-restore
if ($LASTEXITCODE -ne 0) { throw "Dependency audit command failed with exit code $LASTEXITCODE." }
$taskJson = $taskOutput -join [Environment]::NewLine
Set-Content -LiteralPath $taskReportPath -Value $taskJson -Encoding utf8NoBOM
$taskReport = $taskJson | ConvertFrom-Json -AsHashtable
if ($taskReport.version -ne 1 -or -not $taskReport.ContainsKey('projects') -or $taskReport.projects.Count -eq 0) {
    throw 'Dependency audit returned an empty or unsupported report.'
}
if ($taskReport.ContainsKey('problems') -and $taskReport.problems.Count -gt 0) {
    throw "Dependency audit was incomplete. Review $taskReportPath."
}
$taskVulnerabilities = @()
foreach ($taskProject in $taskReport.projects) {
    if ($taskProject.ContainsKey('problems') -and $taskProject.problems.Count -gt 0) {
        throw "Dependency audit was incomplete for $($taskProject.path)."
    }
    if (-not $taskProject.ContainsKey('frameworks')) { continue }
    foreach ($taskFramework in $taskProject.frameworks) {
        foreach ($taskKey in @('topLevelPackages', 'transitivePackages')) {
            if (-not $taskFramework.ContainsKey($taskKey)) { continue }
            foreach ($taskPackage in $taskFramework[$taskKey]) {
                if ($taskPackage.ContainsKey('vulnerabilities') -and $taskPackage.vulnerabilities.Count -gt 0) {
                    $taskVulnerabilities += "$($taskPackage.id) $($taskPackage.resolvedVersion)"
                }
            }
        }
    }
}
if ($taskVulnerabilities.Count -gt 0) {
    throw "Vulnerable direct/transitive dependencies: $($taskVulnerabilities -join ', '). Review $taskReportPath."
}
Write-Output "PASS: NuGet reported no known vulnerable direct/transitive dependencies. Report: $taskReportPath"
