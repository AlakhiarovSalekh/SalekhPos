#requires -Version 7.2
param([switch] $RequireTracked)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskSourcePath = Join-Path $taskRoot 'docs/requirements/final-complete-file-structure.md'
if ((Get-FileHash -LiteralPath $taskSourcePath -Algorithm SHA256).Hash -ne '3648134C078BB553F7D322A4FC03E6750D700A3645F9E16F9F2F81D69E3B7942') {
    throw 'The original user-supplied structure source changed.'
}
$taskEntries = @(Get-Content (Join-Path $taskRoot 'docs/architecture/repository-structure-manifest.json') -Raw | ConvertFrom-Json)
$taskSource = Get-Content (Join-Path $taskRoot 'docs/requirements/final-complete-file-structure.md')
$taskStack = [Collections.Generic.List[string]]::new()
$taskExpected = @(foreach ($taskLine in $taskSource) {
    if ($taskLine -match '^(?<prefix>.*?)[├└]── (?<name>.+)$') {
        if ($Matches.prefix.Length % 4 -ne 0) { throw 'Invalid source indentation.' }
        $taskDepth = $Matches.prefix.Length / 4
        $taskName = $Matches.name.TrimEnd('/')
        while ($taskStack.Count -gt $taskDepth) { $taskStack.RemoveAt($taskStack.Count - 1) }
        if ($taskStack.Count -ne $taskDepth) { throw 'Invalid source tree depth.' }
        $taskDirectory = $Matches.name.EndsWith('/')
        [pscustomobject]@{ path = (@($taskStack) + $taskName) -join '/'; directory = $taskDirectory }
        if ($taskDirectory) { $taskStack.Add($taskName) }
    }
})
if ($taskEntries.Count -ne $taskExpected.Count) { throw 'Manifest does not cover the complete supplied tree.' }
$taskTracked = if ($RequireTracked) { @(& git -C $taskRoot ls-files) } else { @() }
for ($taskIndex = 0; $taskIndex -lt $taskExpected.Count; $taskIndex++) {
    $taskEntry = $taskEntries[$taskIndex]
    $taskOriginal = $taskExpected[$taskIndex]
    if ($taskEntry.path -cne $taskOriginal.path -or $taskEntry.directory -ne $taskOriginal.directory) {
        throw "Manifest differs from supplied tree at entry $taskIndex."
    }
    $taskPath = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskEntry.path))
    if (-not $taskPath.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar)) { throw 'Unsafe manifest path.' }
    $taskType = if ($taskEntry.directory) { 'Container' } else { 'Leaf' }
    if (-not (Test-Path -LiteralPath $taskPath -PathType $taskType)) { throw "Missing required $taskType`: $($taskEntry.path)" }
    if ($RequireTracked) {
        $taskPresent = if ($taskEntry.directory) {
            @($taskTracked | Where-Object { $_.StartsWith($taskEntry.path + '/', [StringComparison]::Ordinal) }).Count -gt 0
        } else { $taskEntry.path -cin $taskTracked }
        if (-not $taskPresent) { throw "Required path will not survive a Git clone: $($taskEntry.path)" }
    }
}
Write-Output "PASS: all $($taskEntries.Count) supplied structure entries exist with their required types."
