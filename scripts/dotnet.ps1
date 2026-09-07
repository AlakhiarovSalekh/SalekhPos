$ErrorActionPreference = 'Stop'
$taskSdk = Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskSdk)) {
    $taskCommand = Get-Command dotnet -ErrorAction Stop
    $taskSdk = $taskCommand.Source
}

# Keep SDK state and build artifacts outside the OneDrive source directory.
$env:DOTNET_CLI_HOME = Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES = Join-Path ([IO.Path]::GetTempPath()) 'salekhpos-nuget-packages'
}
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & $taskSdk @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
