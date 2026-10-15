<#
.SYNOPSIS
    Aplica las migraciones de InstaVende a la base de datos (con respaldo previo automático).

.DESCRIPTION
    - Lee la conexión activa de src/InstaVende.Web/appsettings.Development.json (la línea no comentada),
      o usa -ConnectionString si se indica.
    - Crea la base de datos si no existe.
    - Hace un respaldo COPY_ONLY antes de migrar (se omite con -SinRespaldo o si la base es nueva).
    - Ejecuta database/migraciones.sql (script idempotente: solo aplica lo que falta, se puede repetir).
    No requiere dotnet-ef ni sqlcmd: usa el cliente SQL incluido en Windows PowerShell.

.EXAMPLE
    .\scripts\migrar-bd.ps1
.EXAMPLE
    .\scripts\migrar-bd.ps1 -ConnectionString "Server=.;Database=InstaVendeDb_Dev;User Id=sa;Password=sql;TrustServerCertificate=True"
#>
[CmdletBinding()]
param(
    [string] $ConnectionString,
    [switch] $SinRespaldo
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$appSettings = Join-Path $root 'src\InstaVende.Web\appsettings.Development.json'
$sqlFile = Join-Path $root 'database\migraciones.sql'

function Write-Step([string] $text) { Write-Host "`n▶ $text" -ForegroundColor Cyan }
function Write-Ok([string] $text) { Write-Host "  ✓ $text" -ForegroundColor Green }

# ── 1. Conexión ──────────────────────────────────────────────────────────────
if (-not $ConnectionString) {
    # appsettings admite comentarios // : se eliminan antes de interpretar el JSON
    $lines = Get-Content $appSettings -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*//' }
    $json = ($lines -join "`n") | ConvertFrom-Json
    $ConnectionString = $json.ConnectionStrings.DefaultConnection
    if (-not $ConnectionString) { throw "No hay ConnectionStrings:DefaultConnection activa en $appSettings" }
}

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
$database = $builder['Initial Catalog']
if (-not $database) { throw 'La cadena de conexión no indica la base de datos (Database=...).' }

Write-Host '══════════════════════════════════════════════════' -ForegroundColor DarkGray
Write-Host ' InstaVende · Migración de base de datos' -ForegroundColor White
Write-Host "  Servidor : $($builder['Data Source'])"
Write-Host "  Base     : $database"
Write-Host '══════════════════════════════════════════════════' -ForegroundColor DarkGray

if (-not (Test-Path $sqlFile)) { throw "No se encontró $sqlFile" }

function Invoke-Sql([string] $connString, [string] $sql, [int] $timeout = 600) {
    $conn = New-Object System.Data.SqlClient.SqlConnection $connString
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = $timeout
        $cmd.CommandText = $sql
        return $cmd.ExecuteScalar()
    }
    finally { $conn.Dispose() }
}

# ── 2. Servidor y base de datos ──────────────────────────────────────────────
Write-Step 'Conectando al servidor'
$master = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
$master['Initial Catalog'] = 'master'
$master['Connect Timeout'] = 15
try {
    $version = Invoke-Sql $master.ConnectionString "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50))"
    Write-Ok "SQL Server $version"
}
catch {
    Write-Host "  ✗ No se pudo conectar: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host '    Revisa que SQL Server esté iniciado y que la cadena de conexión activa sea la correcta.' -ForegroundColor Yellow
    exit 1
}

$escapedDb = $database.Replace(']', ']]')
$exists = Invoke-Sql $master.ConnectionString "SELECT COUNT(*) FROM sys.databases WHERE name = N'$($database.Replace("'", "''"))'"
if ($exists -eq 0) {
    Write-Step "La base [$database] no existe: creándola"
    Invoke-Sql $master.ConnectionString "CREATE DATABASE [$escapedDb]" | Out-Null
    Write-Ok 'Base de datos creada'
    $SinRespaldo = $true
}

# ── 3. Respaldo ──────────────────────────────────────────────────────────────
if (-not $SinRespaldo) {
    Write-Step 'Respaldo previo (COPY_ONLY)'
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    # Carpeta de respaldos predeterminada de la instancia (SQL Server 2019+); el servicio de SQL debe poder escribir en ella
    $backupDir = Invoke-Sql $master.ConnectionString "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"
    if (-not $backupDir -or $backupDir -is [System.DBNull]) {
        # Sin carpeta predeterminada (p.ej. LocalDB): se usa database\respaldos del proyecto (servidor local)
        $backupDir = Join-Path $root 'database\respaldos'
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    }
    $backupFile = Join-Path $backupDir "$database-antes-de-migrar-$stamp.bak"
    try {
        Invoke-Sql $master.ConnectionString "BACKUP DATABASE [$escapedDb] TO DISK = N'$($backupFile.Replace("'", "''"))' WITH COPY_ONLY, INIT, NAME = N'InstaVende antes de migrar'" 1800 | Out-Null
        Write-Ok "Respaldo: $backupFile"
    }
    catch {
        Write-Host "  ✗ No se pudo respaldar: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host '    Corrige el problema o vuelve a ejecutar con -SinRespaldo si aceptas migrar sin respaldo.' -ForegroundColor Yellow
        exit 1
    }
}

# ── 4. Migraciones ───────────────────────────────────────────────────────────
Write-Step 'Aplicando migraciones (script idempotente)'
$target = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnectionString
$before = 0
try { $before = Invoke-Sql $target.ConnectionString 'IF OBJECT_ID(N''[__EFMigrationsHistory]'') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM [__EFMigrationsHistory]' } catch { $before = 0 }

$script = Get-Content $sqlFile -Raw -Encoding UTF8
$batches = [regex]::Split($script, '(?im)^\s*GO\s*;?\s*$') | Where-Object { $_.Trim() }
$conn = New-Object System.Data.SqlClient.SqlConnection $target.ConnectionString
try {
    $conn.Open()
    $i = 0
    foreach ($batch in $batches) {
        $i++
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 1800
        $cmd.CommandText = $batch
        try { $cmd.ExecuteNonQuery() | Out-Null }
        catch {
            $detail = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
            Write-Host "  ✗ Error en el lote $i de $($batches.Count): $detail" -ForegroundColor Red
            Write-Host '    La migración en curso se revirtió (cada migración va en su propia transacción).' -ForegroundColor Yellow
            exit 1
        }
    }
}
finally { $conn.Dispose() }

$history = New-Object System.Collections.Generic.List[string]
$conn = New-Object System.Data.SqlClient.SqlConnection $target.ConnectionString
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT MigrationId FROM [__EFMigrationsHistory] ORDER BY MigrationId'
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) { $history.Add($reader.GetString(0)) }
}
finally { $conn.Dispose() }

$applied = $history.Count - $before
Write-Ok "$($batches.Count) lotes ejecutados · $applied migraciones nuevas · $($history.Count) en total"
Write-Host "  Última migración: $($history[$history.Count - 1])" -ForegroundColor DarkGray
Write-Host "`n✅ Base de datos [$database] al día." -ForegroundColor Green
