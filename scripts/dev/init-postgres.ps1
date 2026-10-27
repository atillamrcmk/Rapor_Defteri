$ErrorActionPreference = "Stop"
$bin = "C:\Program Files\PostgreSQL\18\bin"
$root = Join-Path $env:LOCALAPPDATA "RaporDefteri"
$data = Join-Path $root "pgdata"
$secretFile = Join-Path $root "dev-passwords.json"
New-Item -ItemType Directory -Force -Path $root | Out-Null

function New-Secret {
    $alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    $chars = 1..40 | ForEach-Object { $alphabet[(Get-Random -Maximum $alphabet.Length)] }
    return (-join $chars)
}

if (-not (Test-Path -LiteralPath $secretFile)) {
    $secrets = [ordered]@{
        cluster = New-Secret
        migrator = New-Secret
        app = New-Secret
        backup = New-Secret
    }
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $secretFile -Encoding utf8
}
$secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json

if (-not (Test-Path -LiteralPath (Join-Path $data "PG_VERSION"))) {
    $pwFile = Join-Path $root "cluster.pw"
    Set-Content -LiteralPath $pwFile -Value $secrets.cluster -NoNewline -Encoding ascii
    & (Join-Path $bin "initdb.exe") -D $data -U rapor_cluster -E UTF8 --locale=C --pwfile=$pwFile --auth-local=scram-sha-256 --auth-host=scram-sha-256
    Remove-Item -LiteralPath $pwFile -Force
    if ($LASTEXITCODE -ne 0) { throw "initdb başarısız." }
    $conf = Join-Path $data "postgresql.conf"
    Add-Content -LiteralPath $conf -Value @"

listen_addresses = 'localhost'
port = 5433
"@
}

$ctl = Join-Path $bin "pg_ctl.exe"
& $ctl -D $data status | Out-Null
if ($LASTEXITCODE -ne 0) {
    & $ctl -D $data -l (Join-Path $root "postgres.log") start
    if ($LASTEXITCODE -ne 0) { throw "Geliştirme kümesi başlamadı. Mevcut 5432 hizmetine dokunulmadı." }
}

$env:PGPASSWORD = $secrets.cluster
$psql = Join-Path $bin "psql.exe"
function Invoke-Sql([string]$sql) {
    & $psql --host=127.0.0.1 --port=5433 --username=rapor_cluster --dbname=postgres --set=ON_ERROR_STOP=1 --command=$sql
    if ($LASTEXITCODE -ne 0) { throw "SQL başarısız." }
}

$exists = (& $psql --host=127.0.0.1 --port=5433 --username=rapor_cluster --dbname=postgres --tuples-only --no-align --command="SELECT 1 FROM pg_roles WHERE rolname = 'rapor_migrator'" | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw "Küme süper kullanıcısı ile bağlantı kurulamadı. 5432 hizmetine dokunulmadı." }
if ($exists -ne "1") {
    Invoke-Sql "CREATE ROLE rapor_migrator LOGIN PASSWORD '$($secrets.migrator)';"
    Invoke-Sql "CREATE ROLE rapor_app LOGIN PASSWORD '$($secrets.app)';"
    Invoke-Sql "CREATE ROLE rapor_backup LOGIN PASSWORD '$($secrets.backup)';"
    Invoke-Sql "CREATE DATABASE rapor_defteri OWNER rapor_migrator;"
}

Remove-Item Env:PGPASSWORD
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$project = Resolve-Path (Join-Path $PSScriptRoot "..\..\src\RaporDefteri.Web\RaporDefteri.Web.csproj")
$appConn = "Host=127.0.0.1;Port=5433;Database=rapor_defteri;Username=rapor_app;Password=$($secrets.app)"
$migratorConn = "Host=127.0.0.1;Port=5433;Database=rapor_defteri;Username=rapor_migrator;Password=$($secrets.migrator)"
dotnet user-secrets set "ConnectionStrings:Default" $appConn --project $project | Out-Null
dotnet user-secrets set "ConnectionStrings:Migrator" $migratorConn --project $project | Out-Null
Write-Output "Geliştirme kümesi 127.0.0.1:5433 üzerinde. Parolalar bu çıktıda yok: $secretFile"
