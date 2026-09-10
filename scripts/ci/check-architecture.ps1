#requires -Version 7.2
param([string] $RepositoryRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$taskProjects = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'backend'), (Join-Path $taskRoot 'tools/cli'), (Join-Path $taskRoot 'apps/desktop') -Filter '*.csproj' -Recurse)
if ($taskProjects.Count -eq 0) { throw 'No .NET projects were found.' }
$taskSolution = Get-Content -LiteralPath (Join-Path $taskRoot 'SalekhPos.sln') -Raw
$taskSolutionPaths = @([regex]::Matches($taskSolution, '"([^"\r\n]+\.csproj)"') | ForEach-Object {
    [IO.Path]::GetFullPath((Join-Path $taskRoot $_.Groups[1].Value.Replace('\', '/')))
})

function Get-ProjectLayer([string] $Path) {
    $taskRelative = [IO.Path]::GetRelativePath($taskRoot, $Path).Replace('\', '/')
    switch -Regex ($taskRelative) {
        '^backend/src/BuildingBlocks/SalekhPos.SharedKernel/' { return 'SharedKernel' }
        '^backend/src/Modules/([^/]+)/' { return ('Module:' + $Matches[1]) }
        '^backend/src/Bootstrapper/SalekhPos\.(Api|Worker|Migrations)/' { return 'Host' }
        '^tools/cli/SalekhPos.Cli/' { return 'Host' }
        '^apps/desktop/src/SalekhPos\.Desktop\.Domain/' { return 'DesktopDomain' }
        '^apps/desktop/src/SalekhPos\.Desktop\.Application/' { return 'DesktopApplication' }
        '^apps/desktop/src/SalekhPos\.Desktop\.Infrastructure/' { return 'DesktopInfrastructure' }
        '^apps/desktop/tests/' { return 'Tests' }
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
        if ($taskProject.BaseName -match '^SalekhPos\.[^.]+\.(Domain|Contracts|Application|Infrastructure|Api)$') {
            $taskModuleLayer = $Matches[1]
            $taskAllowed = switch ($taskModuleLayer) {
                'Domain' { @() }
                'Contracts' { @() }
                'Application' { @('Domain', 'Contracts') }
                'Infrastructure' { @('Domain', 'Contracts', 'Application') }
                'Api' { @('Domain', 'Contracts', 'Application') }
            }
            $taskReferenceLayer = [IO.Path]::GetFileNameWithoutExtension($taskTarget).Split('.')[-1]
            if ($taskReferenceLayer -notin $taskAllowed) { throw "Forbidden module-layer dependency: $($taskProject.Name) -> $taskTarget" }
        }
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
    if ($taskProject.BaseName -match '\.(Domain|Contracts|Application)$' -and
        $taskXml.SelectNodes('//PackageReference | //FrameworkReference | //Reference').Count -gt 0) {
        throw "Core module layers must remain framework-independent: $($taskProject.Name)"
    }
    if ($taskLayer -ne 'Tests' -and $taskXml.SelectNodes('//Reference').Count -gt 0) {
        throw "Untracked assembly references are not permitted: $($taskProject.Name)"
    }
}
foreach ($taskPath in $taskSolutionPaths) {
    if ($taskPath -notin $taskProjects.FullName) { throw "Solution references an absent project: $taskPath" }
}
# Do not allow a dependency added through a shared import to bypass the project rules.
$taskImports = @(& git -C $taskRoot ls-files --cached --others --exclude-standard -- '*.props' '*.targets' | Sort-Object -Unique)
foreach ($taskImport in $taskImports) {
    $taskFile = Get-Item -LiteralPath (Join-Path $taskRoot $taskImport)
    [xml] $taskXml = Get-Content -LiteralPath $taskFile.FullName -Raw
    if ($taskXml.SelectNodes('//ProjectReference | //Reference').Count -gt 0) {
        throw "Shared imports must not introduce hidden project/assembly dependencies: $($taskFile.FullName)"
    }
}
Write-Output "PASS: architecture and solution coverage checked for $($taskProjects.Count) projects."
