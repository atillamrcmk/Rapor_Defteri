$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$secrets = Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA "RaporDefteri\dev-passwords.json") -Raw | ConvertFrom-Json
$env:ConnectionStrings__Migrator = "Host=127.0.0.1;Port=5433;Database=rapor_defteri;Username=rapor_migrator;Password=$($secrets.migrator)"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $root
try {
    dotnet ef database update --project src/RaporDefteri.Infrastructure/RaporDefteri.Infrastructure.csproj --startup-project src/RaporDefteri.Infrastructure/RaporDefteri.Infrastructure.csproj
    if ($LASTEXITCODE -ne 0) { throw "Migration uygulanamadı." }
}
finally {
    Pop-Location
    Remove-Item Env:ConnectionStrings__Migrator -ErrorAction SilentlyContinue
}
Write-Output "Migration uygulandı."
