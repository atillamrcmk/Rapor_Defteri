$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
Write-Output "DOTNET_ROOT=$env:DOTNET_ROOT"
dotnet --version
