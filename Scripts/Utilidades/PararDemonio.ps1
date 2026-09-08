# Script para detener el demonio de bandeja de sistema
$processes = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*DemonioBarraTareas.ps1*' }
if ($processes) {
    foreach ($proc in $processes) {
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Host "Proceso de demonio detenido (PID $($proc.ProcessId))."
        } catch {}
    }
} else {
    Write-Host "No hay instancias activas del demonio de la barra de tareas."
}
