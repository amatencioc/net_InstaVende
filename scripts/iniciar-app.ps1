<#
.SYNOPSIS
    Compila y levanta InstaVende (API + panel web + servicio de WhatsApp) en cualquier PC con Windows.

.DESCRIPTION
    1. Verifica requisitos: .NET SDK 10 y Node.js 20.19+ (Chrome o Edge para WhatsApp QR, opcional).
    2. Instala dependencias (NuGet, frontend y wa-client) y compila la solución.
    3. Migra la base de datos activa en appsettings.Development.json (con respaldo), salvo -SinMigrar.
    4. Opcional: genera datos de demostración (-SeedDemo correo@dominio).
    5. Abre la API (http://localhost:5221) y el panel (http://localhost:5173) en ventanas propias y el navegador.

.EXAMPLE
    .\scripts\iniciar-app.ps1
.EXAMPLE
    .\scripts\iniciar-app.ps1 -SeedDemo demo@instavende.pe
.EXAMPLE
    .\scripts\iniciar-app.ps1 -SoloCompilar
#>
[CmdletBinding()]
param(
    [switch] $SinMigrar,
    [switch] $SinRespaldo,
    [string] $SeedDemo,
    [switch] $SoloCompilar,
    [switch] $SinNavegador
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src\InstaVende.Web'
$frontend = Join-Path $root 'frontend'
$waClient = Join-Path $root 'wa-client'
$apiUrl = 'http://localhost:5221'
$webUrl = 'http://localhost:5173'

function Write-Step([string] $text) { Write-Host "`n▶ $text" -ForegroundColor Cyan }
function Write-Ok([string] $text) { Write-Host "  ✓ $text" -ForegroundColor Green }
function Write-Warn([string] $text) { Write-Host "  ! $text" -ForegroundColor Yellow }
function Fail([string] $text) { Write-Host "  ✗ $text" -ForegroundColor Red; exit 1 }

function Invoke-Checked([string] $what, [scriptblock] $action) {
    # Los programas externos (npm, dotnet) escriben avisos en stderr: se juzgan solo por su código de salida
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $action 2>&1 | ForEach-Object { "    $_" } }
    finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { Fail "$what falló (código $LASTEXITCODE)." }
}

Write-Host '══════════════════════════════════════════════════' -ForegroundColor DarkGray
Write-Host ' InstaVende · Compilar y levantar' -ForegroundColor White
Write-Host '══════════════════════════════════════════════════' -ForegroundColor DarkGray

# ── 1. Requisitos ────────────────────────────────────────────────────────────
Write-Step 'Verificando requisitos'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail 'Falta el SDK de .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0'
}
$sdks = (& dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '(?m)^10\.') { Fail "Se necesita el SDK de .NET 10 (instalados: $($sdks -replace "`n", ', ')). Descárgalo en https://dotnet.microsoft.com/download/dotnet/10.0" }
Write-Ok ".NET SDK $((& dotnet --version))"

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Fail 'Falta Node.js 22 LTS: https://nodejs.org' }
$nodeVersion = [version]((& node --version).TrimStart('v'))
if ($nodeVersion -lt [version]'20.19.0') { Fail "Node.js $nodeVersion es antiguo; instala Node.js 22 LTS: https://nodejs.org" }
Write-Ok "Node.js $nodeVersion"

$browser = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if ($browser) { Write-Ok "Navegador para WhatsApp QR: $browser" }
else { Write-Warn 'No se encontró Chrome ni Edge: el QR de WhatsApp no funcionará (el resto de la app sí).' }

# ── 2. Dependencias y compilación ────────────────────────────────────────────
Write-Step 'Instalando dependencias del panel web (npm ci)'
Push-Location $frontend
try { Invoke-Checked 'npm ci (frontend)' { npm ci --no-audit --no-fund } } finally { Pop-Location }
Write-Ok 'frontend listo'

Write-Step 'Instalando dependencias del servicio de WhatsApp (npm ci)'
Push-Location $waClient
try {
    # Se usa el Chrome/Edge del sistema: no hace falta descargar Chromium
    $env:PUPPETEER_SKIP_DOWNLOAD = 'true'
    Invoke-Checked 'npm ci (wa-client)' { npm ci --no-audit --no-fund }
}
finally { Pop-Location }
Write-Ok 'wa-client listo'

Write-Step 'Compilando la solución (.NET)'
Push-Location $root
try { Invoke-Checked 'dotnet build' { dotnet build InstaVende.slnx -nologo -v minimal } } finally { Pop-Location }
Write-Ok 'Compilación correcta'

if ($SoloCompilar) {
    Write-Host "`n✅ Compilado. Ejecuta el script sin -SoloCompilar para levantar la app." -ForegroundColor Green
    exit 0
}

# ── 3. Base de datos ─────────────────────────────────────────────────────────
if (-not $SinMigrar) {
    Write-Step 'Migrando la base de datos'
    & (Join-Path $PSScriptRoot 'migrar-bd.ps1') -SinRespaldo:$SinRespaldo
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { Fail 'La migración falló; la app no se levantará.' }
}

if ($SeedDemo) {
    Write-Step "Generando datos de demostración para $SeedDemo"
    Push-Location $root
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        Invoke-Checked 'Datos de demostración' { dotnet run --project $webProject --no-build --no-launch-profile -- --seed-demo $SeedDemo }
    }
    finally { Pop-Location }
    Write-Ok 'Datos de demostración generados'
}

# ── 4. Levantar servicios ────────────────────────────────────────────────────
function Test-PortInUse([int] $port) {
    return [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}
foreach ($port in 5221, 5173) {
    if (Test-PortInUse $port) { Fail "El puerto $port ya está en uso. Ejecuta .\scripts\detener-app.ps1 y vuelve a intentarlo." }
}

Write-Step 'Levantando la API (ventana nueva)'
$apiCommand = @(
    "`$Host.UI.RawUI.WindowTitle = 'InstaVende · API $apiUrl'"
    "Set-Location '$root'"
    $(if ($browser) { "`$env:PUPPETEER_EXECUTABLE_PATH = '$browser'" })
    "dotnet run --project '$webProject' --launch-profile http --no-build"
) -join '; '
Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', $apiCommand | Out-Null

Write-Step 'Levantando el panel web (ventana nueva)'
$webCommand = "`$Host.UI.RawUI.WindowTitle = 'InstaVende · Panel $webUrl'; Set-Location '$frontend'; npm run dev"
Start-Process powershell -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', $webCommand | Out-Null

Write-Step 'Esperando a que los servicios respondan'
$deadline = (Get-Date).AddMinutes(3)
$apiUp = $false; $webUp = $false
while ((Get-Date) -lt $deadline -and -not ($apiUp -and $webUp)) {
    Start-Sleep -Seconds 2
    if (-not $apiUp) { try { $apiUp = (Invoke-WebRequest "$apiUrl/health" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { } }
    if (-not $webUp) { try { $webUp = (Invoke-WebRequest $webUrl -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { } }
}
if ($apiUp) { Write-Ok "API      → $apiUrl  (documentación: $apiUrl/api/docs)" } else { Write-Warn 'La API no respondió a tiempo: revisa su ventana.' }
if ($webUp) { Write-Ok "Panel    → $webUrl" } else { Write-Warn 'El panel no respondió a tiempo: revisa su ventana.' }

if ($apiUp -and $webUp -and -not $SinNavegador) { Start-Process "$webUrl/login" }

Write-Host "`n✅ InstaVende en marcha. Para detenerlo: .\scripts\detener-app.ps1 (o cierra las dos ventanas)." -ForegroundColor Green
