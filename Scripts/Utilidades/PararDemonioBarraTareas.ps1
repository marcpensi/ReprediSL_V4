# Script para forzar la detencion y limpieza completa del demonio de bandeja de sistema.
Add-Type -AssemblyName System.Windows.Forms

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " DETENIENDO Y LIMPIANDO DEMONIO DE BANDEJA (REPREDISL V4)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

$count = 0

# 1. Buscar todos los procesos PowerShell que contengan 'DemonioBarraTareas'
try {
    $processes = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*DemonioBarraTareas*' }
    if ($processes) {
        foreach ($proc in $processes) {
            try {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
                Write-Host " [OK] Proceso demonio detenido (PID $($proc.ProcessId))." -ForegroundColor Green
                $count++
            } catch {
                Write-Host " [AVISO] No se pudo detener el proceso PID $($proc.ProcessId)." -ForegroundColor Yellow
            }
        }
    }
} catch {
    # Fallback por Get-Process si CIM falla
    Get-Process powershell -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.MainWindowTitle -like '*Demonio*' -or $_.CommandLine -like '*Demonio*') {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            $count++
        }
    }
}

if ($count -eq 0) {
    Write-Host " [INFO] No habia ninguna instancia activa del demonio en ejecucion." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host " [EXITO] Se han detenido $count instancia(s) del demonio correctamente." -ForegroundColor Green
}

Write-Host ""
