# Script para forzar la detencion y limpieza completa del demonio de bandeja de sistema.
Add-Type -AssemblyName System.Windows.Forms

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " DETENIENDO Y LIMPIANDO DEMONIO DE BANDEJA (REPREDISL V4)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

$count = 0
$myPid = $PID

# 1. Buscar mediante Win32_Process (Comprobacion de Linea de Comandos)
# Excluyendo explicitamente este propio proceso y cualquier llamada a 'PararDemonio'
try {
    $processes = Get-CimInstance Win32_Process | Where-Object { 
        $_.CommandLine -and 
        $_.ProcessId -ne $myPid -and 
        $_.CommandLine -notlike '*PararDemonio*' -and 
        ($_.CommandLine -like '*DemonioBarraTareas.ps1*' -or ($_.CommandLine -like '*DemonioBarraTareas*' -and $_.CommandLine -notlike '*Parar*'))
    }
    if ($processes) {
        foreach ($proc in $processes) {
            try {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
                Write-Host " [OK] Proceso demonio detenido (PID $($proc.ProcessId))." -ForegroundColor Green
                $count++
            } catch {}
        }
    }
} catch {}

# 2. Buscar mediante Titulo de Ventana (Fallback)
Get-Process powershell, pwsh -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Id -ne $myPid) {
        if ($_.MainWindowTitle -like '*Demonio de Bandeja*' -or $_.MainWindowTitle -like '*Demonio de Sincronizacion*') {
            try {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
                Write-Host " [OK] Proceso demonio detenido por Titulo de Ventana (PID $($_.Id))." -ForegroundColor Green
                $count++
            } catch {}
        }
    }
}

if ($count -eq 0) {
    Write-Host " [INFO] No habia ninguna instancia activa del demonio en ejecucion." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host " [EXITO] Se han detenido $count instancia(s) del demonio de una sola vez." -ForegroundColor Green
}

Write-Host ""
