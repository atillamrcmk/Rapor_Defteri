param(
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin",
    [string]$HostName = "127.0.0.1",
    [int]$Port = 5432,
    [string]$Database = "rapor_defteri",
    [string]$Username = "rapor_backup",
    [Parameter(Mandatory = $true)][string]$PrimaryDir,
    [Parameter(Mandatory = $true)][string]$SecondaryDir
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
    throw "PGPASSWORD ortam değişkeni boş. Betik parolayı ekrana yazmaz."
}
if ((Resolve-Path -LiteralPath $PrimaryDir -ErrorAction SilentlyContinue)?.Path -eq (Resolve-Path -LiteralPath $SecondaryDir -ErrorAction SilentlyContinue)?.Path) {
    throw "İkinci yedek yolu birinciden farklı olmalıdır."
}

New-Item -ItemType Directory -Force -Path $PrimaryDir | Out-Null
New-Item -ItemType Directory -Force -Path $SecondaryDir | Out-Null
if (-not (Test-Path -LiteralPath $SecondaryDir)) {
    throw "İkinci yedek yolu yok. backup_log yazılmadı."
}

$name = "rapor_defteri-{0:yyyyMMdd-HHmmss}.dump" -f [DateTime]::UtcNow
$primary = Join-Path $PrimaryDir $name
$dump = Join-Path $PgBin "pg_dump.exe"
$restore = Join-Path $PgBin "pg_restore.exe"
$psql = Join-Path $PgBin "psql.exe"

& $dump --host=$HostName --port=$Port --username=$Username --dbname=$Database --format=custom --file=$primary
if ($LASTEXITCODE -ne 0) { throw "pg_dump başarısız. backup_log yazılmadı." }

$secondary = Join-Path $SecondaryDir $name
Copy-Item -LiteralPath $primary -Destination $secondary -Force
& $restore --list $secondary | Out-Null
if ($LASTEXITCODE -ne 0) { throw "pg_restore --list başarısız. backup_log yazılmadı." }

$size = (Get-Item -LiteralPath $secondary).Length
$sql = @"
INSERT INTO rapor.backup_log (id, verified_at, file_name, size_bytes, primary_path, secondary_path, check_command)
VALUES (gen_random_uuid(), now(), :'file_name', $size, :'primary_path', :'secondary_path', 'pg_restore --list');
"@
& $psql --host=$HostName --port=$Port --username=$Username --dbname=$Database --set=ON_ERROR_STOP=1 --set=file_name=$name --set=primary_path=$primary --set=secondary_path=$secondary --command=$sql
if ($LASTEXITCODE -ne 0) { throw "Döküm alındı fakat backup_log yazılamadı." }
Write-Output "Yedek doğrulandı: $name ($size bayt). İki yola yazıldı."
