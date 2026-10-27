$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}
$secrets = Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA "RaporDefteri\dev-passwords.json") -Raw | ConvertFrom-Json
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$env:PGPASSWORD = $secrets.migrator
& $psql --host=127.0.0.1 --port=5433 --username=rapor_migrator --dbname=rapor_defteri --set=ON_ERROR_STOP=1 --file (Join-Path $root "scripts\db\grant-app.sql")
if ($LASTEXITCODE -ne 0) { throw "Yetki betiği başarısız." }
$env:PGPASSWORD = $secrets.app
$ErrorActionPreference = "Continue"
$denied = & $psql --host=127.0.0.1 --port=5433 --username=rapor_app --dbname=rapor_defteri --command="DELETE FROM rapor.report_entry WHERE false;" 2>&1 | Out-String
$ErrorActionPreference = "Stop"
if ($denied -notmatch "permission denied") { throw "rapor_app silme yetkisi beklenenden geniş: $denied" }
$ErrorActionPreference = "Continue"
$updateDenied = & $psql --host=127.0.0.1 --port=5433 --username=rapor_app --dbname=rapor_defteri --command="UPDATE rapor.audit_event SET details = details WHERE false;" 2>&1 | Out-String
$ErrorActionPreference = "Stop"
if ($updateDenied -notmatch "permission denied") { throw "Denetim satırı güncellenebiliyor: $updateDenied" }
Remove-Item Env:PGPASSWORD
Write-Output "rapor_app silme ve denetim güncelleme reddedildi."
