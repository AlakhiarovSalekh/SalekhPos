#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskReport = Get-Content -LiteralPath (Join-Path $taskRoot 'docs/architecture/master-specification-coverage.md') -Raw
$taskSources = @(
    @{ Id = 'A'; File = 'complete-codex-master-prompt.md'; Count = 144; Hash = '5AA3CA0E21E8B382C89E483A90A46AC9FE468DB6DF1DF7D4AC23382C049DA039' },
    @{ Id = 'B'; File = 'master-engineering-specification-v2.md'; Count = 152; Hash = 'D326A2E695462336AFA3C0F6C17C410B2C18228C3AD9CFE3F6A6F8C15AE1389F' },
    @{ Id = 'C'; File = 'master-architecture-charter.md'; Count = 168; Hash = 'B9CF83F9DACAFACAB861C7554901FE27451041602290F9D385ECB88B778FA8CB' }
)
foreach ($taskSource in $taskSources) {
    if ($taskSource.Id -eq 'C') {
        $taskReport = Get-Content -LiteralPath (Join-Path $taskRoot 'docs/architecture/current-charter-index.md') -Raw
    }
    $taskPath = Join-Path $taskRoot ('docs/requirements/' + $taskSource.File)
    if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskSource.Hash) {
        throw "Original requirement copy changed: $($taskSource.File)"
    }
    $taskLines = @(Get-Content -LiteralPath $taskPath)
    $taskSections = @()
    for ($taskIndex = 0; $taskIndex -lt $taskLines.Count; $taskIndex++) {
        if ($taskLines[$taskIndex] -match '^# (?:PART )?(\d+)(?:\.| —) (.+)$') {
            $taskSections += @{ Number = [int]$Matches[1]; Title = $Matches[2].Replace('|', '/'); Start = $taskIndex + 1 }
        }
    }
    if ($taskSections.Count -ne $taskSource.Count) { throw 'Requirement section count changed.' }
    for ($taskIndex = 0; $taskIndex -lt $taskSections.Count; $taskIndex++) {
        $taskSection = $taskSections[$taskIndex]
        $taskEnd = if ($taskIndex + 1 -lt $taskSections.Count) { $taskSections[$taskIndex + 1].Start - 1 } else { $taskLines.Count }
        $taskExpected = "| $($taskSource.Id)$($taskSection.Number) | $($taskSection.Title) | $($taskSection.Start)–$taskEnd |"
        if ([regex]::Matches($taskReport, [regex]::Escape($taskExpected)).Count -ne 1) {
            throw "Missing, duplicate or incorrect requirement range: $($taskSource.Id)$($taskSection.Number)"
        }
    }
}
if ($taskReport -match '<!-- (MATRIX_|AUDIT_VALIDATION)') { throw 'Unfinished requirement matrix.' }
Write-Output 'PASS: original requirement hashes and all 464 section ranges verified.'
