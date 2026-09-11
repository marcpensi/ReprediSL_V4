@echo off
setlocal EnableDelayedExpansion

set "ARG=%~1"

if /i "%ARG%"=="-TODO" goto PARAR_TODO
if /i "%ARG%"=="TODO" goto PARAR_TODO
if /i "%ARG%"=="-ALL" goto PARAR_TODO
if /i "%ARG%"=="ALL" goto PARAR_TODO
if /i "%ARG%"=="/TODO" goto PARAR_TODO
if /i "%ARG%"=="--TODO" goto PARAR_TODO

:MENU
cls
echo ================================================================================
echo   REPREDISL V4 - PANEL DE DETENCION DE SERVICIOS
echo ================================================================================
echo.
echo   Estado actual de los procesos:
powershell -NoProfile -Command "$procs = @('ReprediTrayDaemon', 'sincronizador', 'postgrest', 'caddy'); foreach ($pr in $procs) { $found = Get-Process -Name $pr -ErrorAction SilentlyContinue; if ($found) { Write-Host ('    [ACTIVO]   ' + $pr) -ForegroundColor Green } else { Write-Host ('    [DETENIDO] ' + $pr) -ForegroundColor DarkGray } }"
echo.
echo --------------------------------------------------------------------------------
echo   Selecciona una opcion:
echo --------------------------------------------------------------------------------
echo     1. Detener Centro de Control (ReprediTrayDaemon)
echo     2. Detener Sincronizador de Pedidos (sincronizador / script)
echo     3. Detener API PostgREST (postgrest)
echo     4. Detener Servidor Proxy HTTPS (caddy)
echo     5. Detener Servicio Windows PostgreSQL
echo     6. Detener TODOS los servicios de ReprediSL (Equivalente a PARAR -TODO)
echo     0. Salir
echo --------------------------------------------------------------------------------
echo.
set /p OPCION="Introduce una opcion [0-6]: "

if "%OPCION%"=="1" goto PARAR_DAEMON
if "%OPCION%"=="2" goto PARAR_SYNC
if "%OPCION%"=="3" goto PARAR_POSTGREST
if "%OPCION%"=="4" goto PARAR_CADDY
if "%OPCION%"=="5" goto PARAR_POSTGRES
if "%OPCION%"=="6" goto PARAR_TODO
if "%OPCION%"=="0" goto SALIR

echo.
echo Opción no válida.
timeout /t 1 > nul
goto MENU

:PARAR_DAEMON
echo.
echo Deteniendo ReprediTrayDaemon...
taskkill /F /IM ReprediTrayDaemon.exe > nul 2>&1
echo [OK] ReprediTrayDaemon detenido.
echo.
pause
goto MENU

:PARAR_SYNC
echo.
echo Deteniendo Sincronizador de Pedidos...
taskkill /F /IM sincronizador.exe > nul 2>&1
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*SincronizarPedidosEntrantes.ps1*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" > nul 2>&1
echo [OK] Sincronizador detenido.
echo.
pause
goto MENU

:PARAR_POSTGREST
echo.
echo Deteniendo API PostgREST...
taskkill /F /IM postgrest.exe > nul 2>&1
echo [OK] PostgREST detenido.
echo.
pause
goto MENU

:PARAR_CADDY
echo.
echo Deteniendo Caddy Reverse Proxy...
taskkill /F /IM caddy.exe > nul 2>&1
echo [OK] Caddy detenido.
echo.
pause
goto MENU

:PARAR_POSTGRES
echo.
echo Deteniendo servicio PostgreSQL...
set "PG_SERVICE="
for /f "usebackq delims=" %%A in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$l = Join-Path '%~dp0' 'Scripts\Despliegue\Load-PsForceConfig.ps1'; if (Test-Path $l) { $c = & $l; Write-Output ('PG_SERVICE=' + $c.PostgreSQL.ServiceName); }"`) do (
    set "%%A"
)
if "%PG_SERVICE%"=="" set "PG_SERVICE=postgresql-x64-17"
net stop "%PG_SERVICE%" > nul 2>&1
powershell -NoProfile -Command "Get-Service | Where-Object { $_.Name -like '*postgres*' -and $_.Status -eq 'Running' } | Stop-Service -Force -ErrorAction SilentlyContinue" > nul 2>&1
echo [OK] Servicio PostgreSQL (%PG_SERVICE%) solicitado detener.
echo.
pause
goto MENU

:PARAR_TODO
echo.
echo ================================================================================
echo   DETENIENDO TODOS LOS SERVICIOS DE REPREDISL...
echo ================================================================================
echo   - Deteniendo ReprediTrayDaemon...
taskkill /F /IM ReprediTrayDaemon.exe > nul 2>&1

echo   - Deteniendo Sincronizador de Pedidos...
taskkill /F /IM sincronizador.exe > nul 2>&1
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*SincronizarPedidosEntrantes.ps1*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" > nul 2>&1

echo   - Deteniendo PostgREST...
taskkill /F /IM postgrest.exe > nul 2>&1

echo   - Deteniendo Caddy...
taskkill /F /IM caddy.exe > nul 2>&1

echo.
echo ================================================================================
echo   [OK] Todos los servicios de ReprediSL han sido detenidos limpiamente.
echo ================================================================================
echo.
if /i "%ARG%"=="-TODO" exit /b 0
if /i "%ARG%"=="TODO" exit /b 0
if /i "%ARG%"=="-ALL" exit /b 0
if /i "%ARG%"=="ALL" exit /b 0
pause
exit /b 0

:SALIR
echo.
echo Saliendo sin cambios.
exit /b 0
