#requires -Version 7.2
param([string] $RepositoryRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$taskProjects = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'backend') -Filter '*.csproj' -Recurse)
if ($taskProjects.Count -eq 0) { throw 'No .NET projects were found.' }
[xml] $taskSolution = Get-Content -LiteralPath (Join-Path $taskRoot 'SalekhPos.slnx') -Raw
$taskSolutionPaths = @($taskSolution.SelectNodes('//Project') | ForEach-Object {
    [IO.Path]::GetFullPath((Join-Path $taskRoot $_.GetAttribute('Path')))
})

function Get-ProjectLayer([string] $Path) {
    $taskRelative = [IO.Path]::GetRelativePath($taskRoot, $Path).Replace('\', '/')
    switch -Regex ($taskRelative) {
        '^backend/src/BuildingBlocks/SalekhPos.SharedKernel/' { return 'SharedKernel' }
        '^backend/src/Modules/([^/]+)/' { return ('Module:' + $Matches[1]) }
        '^backend/src/Bootstrapper/SalekhPos\.(Api|Worker)/' { return 'Host' }
        '^backend/tests/' { return 'Tests' }
        default { throw "Unclassified project location: $taskRelative. Extend the architecture rule explicitly." }
    }
}

foreach ($taskProject in $taskProjects) {
    $taskLayer = Get-ProjectLayer $taskProject.FullName
    if ($taskProject.FullName -notin $taskSolutionPaths) {
        throw "Project omitted from the solution and CI: $($taskProject.FullName)"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $taskProject.DirectoryName 'packages.lock.json'))) {
        throw "Missing committed package lock file for $($taskProject.Name)"
    }
    [xml] $taskXml = Get-Content -LiteralPath $taskProject.FullName -Raw
    foreach ($taskReference in $taskXml.SelectNodes('//ProjectReference')) {
        $taskInclude = $taskReference.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($taskInclude) -or $taskInclude -match '[$*?;]') {
            throw "Project references must use explicit paths: $($taskProject.Name)"
        }
        $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskProject.DirectoryName $taskInclude))
        if ($taskTarget -notin $taskProjects.FullName) { throw "Unknown project reference: $taskTarget" }
        $taskTargetLayer = Get-ProjectLayer $taskTarget
        if ($taskLayer -eq 'SharedKernel' -or
            ($taskLayer -ne 'Tests' -and $taskTargetLayer -eq 'Tests') -or
            ($taskLayer.StartsWith('Module:') -and $taskTargetLayer -ne 'SharedKernel' -and $taskTargetLayer -ne $taskLayer)) {
            throw "Forbidden project dependency: $($taskProject.Name) ($taskLayer) -> $taskTarget ($taskTargetLayer)"
        }
    }
    if ($taskLayer -eq 'SharedKernel' -and
        $taskXml.SelectNodes('//PackageReference | //FrameworkReference | //Reference').Count -gt 0) {
        throw 'SharedKernel must remain independent of external packages and application infrastructure.'
    }
    if ($taskLayer -ne 'Tests' -and $taskXml.SelectNodes('//Reference').Count -gt 0) {
        throw "Untracked assembly references are not permitted: $($taskProject.Name)"
    }
}
foreach ($taskPath in $taskSolutionPaths) {
    if ($taskPath -notin $taskProjects.FullName) { throw "Solution references an absent project: $taskPath" }
}
# Do not allow a dependency added through a shared import to bypass the project rules.
foreach ($taskFile in Get-ChildItem -LiteralPath $taskRoot -File -Recurse -Include '*.props', '*.targets') {
    if ($taskFile.FullName -match '[/\\](bin|obj|\.git)[/\\]') { continue }
    [xml] $taskXml = Get-Content -LiteralPath $taskFile.FullName -Raw
    if ($taskXml.SelectNodes('//ProjectReference | //Reference').Count -gt 0) {
        throw "Shared imports must not introduce hidden project/assembly dependencies: $($taskFile.FullName)"
    }
}
Write-Output "PASS: architecture and solution coverage checked for $($taskProjects.Count) projects."
