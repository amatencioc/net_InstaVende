<#
.SYNOPSIS
    Detiene la API (5221), el panel web (5173) y el servicio de WhatsApp (3001) de InstaVende.
#>
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$services = @{ 5221 = 'API'; 5173 = 'Panel web'; 3001 = 'Servicio de WhatsApp' }
foreach ($port in $services.Keys | Sort-Object) {
    $owners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    if (-not $owners) { Write-Host "  · $($services[$port]) ($port): no estaba en marcha" -ForegroundColor DarkGray; continue }
    foreach ($processId in $owners) {
        # /T detiene también los procesos hijos (p.ej. Chrome del servicio de WhatsApp)
        & taskkill /PID $processId /T /F | Out-Null
    }
    Write-Host "  ✓ $($services[$port]) ($port) detenido" -ForegroundColor Green
}
