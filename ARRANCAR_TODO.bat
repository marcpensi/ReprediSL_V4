@echo off
chcp 65001 > nul
setlocal EnableDelayedExpansion

set "ARG=%~1"

if /i "%ARG%"=="debug" goto MODO_DEBUG
if /i "%ARG%"=="-debug" goto MODO_DEBUG
if /i "%ARG%"=="/debug" goto MODO_DEBUG
if /i "%ARG%"=="--debug" goto MODO_DEBUG

rem ================================================================================
rem   MODO NORMAL (SIN MOLESTAR - PUESTA EN MARCHA DIRECTA EN SEGUNDO PLANO)
rem ================================================================================
title ReprediSL V4 - Centro de Control y Servicios
echo ================================================================================
echo   REPREDISL V4 - CENTRO DE CONTROL Y SERVICIOS
echo ================================================================================
echo.
echo   Iniciando Centro de Control en segundo plano...
echo   Servicios gestionados:
echo     • PostgreSQL (repredisl_api)
echo     • PostgREST API
echo     • Caddy Reverse Proxy (HTTPS / SSL)
echo     • Sincronizador de Pedidos a Access ERP
echo.

set "DAEMON_EXE="
if exist "%~dp0src\Daemon\TrayDaemon\TrayDaemon.exe" set "DAEMON_EXE=%~dp0src\Daemon\TrayDaemon\TrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "%~dp0TrayDaemon\TrayDaemon.exe" set "DAEMON_EXE=%~dp0TrayDaemon\TrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "%~dp0src\Daemon\bin\ReprediTrayDaemon.exe" set "DAEMON_EXE=%~dp0src\Daemon\bin\ReprediTrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "%~dp0ReprediTrayDaemon\ReprediTrayDaemon.exe" set "DAEMON_EXE=%~dp0ReprediTrayDaemon\ReprediTrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "%~dp0src\Daemon\ReprediTrayDaemon\bin\Release\net10.0-windows\ReprediTrayDaemon.exe" set "DAEMON_EXE=%~dp0src\Daemon\ReprediTrayDaemon\bin\Release\net10.0-windows\ReprediTrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "C:\pensi\psforce\TrayDaemon\TrayDaemon.exe" set "DAEMON_EXE=C:\pensi\psforce\TrayDaemon\TrayDaemon.exe"
if "%DAEMON_EXE%"=="" if exist "C:\pensi\psforce\ReprediTrayDaemon\ReprediTrayDaemon.exe" set "DAEMON_EXE=C:\pensi\psforce\ReprediTrayDaemon\ReprediTrayDaemon.exe"

if "%DAEMON_EXE%"=="" (
    echo [ERROR] No se encuentra TrayDaemon.exe ni ReprediTrayDaemon.exe en:
    echo   - %~dp0src\Daemon\TrayDaemon\TrayDaemon.exe
    echo   - %~dp0src\Daemon\bin\ReprediTrayDaemon.exe
    echo.
    echo Ejecuta: dotnet build "%~dp0src\Daemon\ReprediTrayDaemon\ReprediTrayDaemon.csproj"
    pause
    exit /b 1
)

start "" "%DAEMON_EXE%" --start-all

echo ================================================================================
echo   [OK] Centro de Control iniciado.
echo   El icono de Repredi aparecerá en la barra de tareas junto al reloj.
echo ================================================================================
echo.
timeout /t 3 /nobreak > nul
exit /b 0

rem ================================================================================
rem   MODO DEBUG (INTERACTIVO - PASO A PASO CON PREGUNTAS Y DIAGNÓSTICO)
rem ================================================================================
:MODO_DEBUG
title ReprediSL V4 - Arranque Interactivo [MODO DEBUG]
cls
echo ================================================================================
echo   REPREDISL V4 - ARRANQUE INTERACTIVO [MODO DEBUG]
echo ================================================================================
echo   Este modo ejecutará cada fase paso a paso, verificando componentes
echo   y solicitando confirmación antes de proceder.
echo ================================================================================
echo.
pause

rem --- PASO 1: VERIFICAR RUTAS Y BINARIOS ---
echo.
echo --------------------------------------------------------------------------------
echo [PASO 1/5] Verificación de Binarios y Componentes
echo --------------------------------------------------------------------------------
rem Cargar configuracion previa
set "CFG_FILE=%~dp0config.json"
if not exist "%CFG_FILE%" if exist "%PSFORCE_CONFIG%" set "CFG_FILE=%PSFORCE_CONFIG%"
if not exist "%CFG_FILE%" set "CFG_FILE=C:\pensi\psforce\config.json"

for /f "usebackq delims=" %%A in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$l = Join-Path '%~dp0' 'Scripts\Despliegue\Load-PsForceConfig.ps1'; if (Test-Path $l) { $c = & $l -ConfigFile '%CFG_FILE%'; Write-Output ('ACCESS_MDB=' + $c.AccessDbPath); Write-Output ('PG_PORT=' + $c.PostgreSQL.Port); }"`) do (
    set "%%A"
)

set "DAEMON_DEV=%~dp0src\Daemon\TrayDaemon\TrayDaemon.exe"
if not exist "%DAEMON_DEV%" set "DAEMON_DEV=%~dp0src\Daemon\bin\ReprediTrayDaemon.exe"
set "DAEMON_CLI=C:\pensi\psforce\TrayDaemon\TrayDaemon.exe"
if not exist "%DAEMON_CLI%" set "DAEMON_CLI=C:\pensi\psforce\ReprediTrayDaemon\ReprediTrayDaemon.exe"
set "SYNC_CLI=C:\pensi\psforce\Sync\sincronizador.exe"
set "SYNC_DEV=%~dp0src\Sync\ReprediSync\bin\Release\net10.0-windows\sincronizador.exe"
set "POSTGREST_EXE=%~dp0src\API\postgrest.exe"

set "EXE_TO_RUN="
if exist "%DAEMON_DEV%" (
    echo   [OK] ReprediTrayDaemon (Dev)   : %DAEMON_DEV%
    set "EXE_TO_RUN=%DAEMON_DEV%"
) else if exist "%DAEMON_CLI%" (
    echo   [OK] ReprediTrayDaemon (Client): %DAEMON_CLI%
    set "EXE_TO_RUN=%DAEMON_CLI%"
) else (
    echo   [ALERTA] ReprediTrayDaemon.exe NO encontrado.
)

if exist "%SYNC_CLI%" (
    echo   [OK] Sincronizador (.exe)      : %SYNC_CLI%
) else if exist "%SYNC_DEV%" (
    echo   [OK] Sincronizador (.exe Dev)  : %SYNC_DEV%
) else (
    echo   [INFO] Sincronizador por script PowerShell disponible.
)

if exist "%POSTGREST_EXE%" (
    echo   [OK] PostgREST API             : %POSTGREST_EXE%
) else (
    echo   [ALERTA] postgrest.exe no localizado en src\API
)

if exist "%ACCESS_MDB%" (
    echo   [OK] Base ERP Access           : %ACCESS_MDB%
) else (
    echo   [ALERTA] No se encuentra gestion.mdb en %ACCESS_MDB%
)

echo.
set /p "RESP1=¿Deseas continuar al PASO 2 (Comprobar Conexión PostgreSQL)? [S/N, Enter=S]: "
if /i "%RESP1%"=="N" goto CANCELADO

rem --- PASO 2: VERIFICAR POSTGRESQL ---
echo.
echo --------------------------------------------------------------------------------
echo [PASO 2/5] Verificación del Servicio PostgreSQL
echo --------------------------------------------------------------------------------
if "%PG_PORT%"=="" set "PG_PORT=5433"
echo   Comprobando puerto configurado (%PG_PORT%)...
powershell -NoProfile -Command "$client = New-Object System.Net.Sockets.TcpClient; try { $client.Connect('127.0.0.1', %PG_PORT%); Write-Host '  [OK] PostgreSQL respondiendo en puerto %PG_PORT%' -ForegroundColor Green; $client.Close() } catch { Write-Host '  [-] Puerto %PG_PORT% inactivo' -ForegroundColor DarkGray }"
echo.
set /p "RESP2=¿Deseas continuar al PASO 3 (Inspeccionar Configuración)? [S/N, Enter=S]: "
if /i "%RESP2%"=="N" goto CANCELADO

rem --- PASO 3: INSPECCIONAR CONFIGURACIÓN ---
echo.
echo --------------------------------------------------------------------------------
echo [PASO 3/5] Configuración de Despliegue
echo --------------------------------------------------------------------------------
if exist "%CFG_FILE%" (
    echo   Leyendo configuración de: %CFG_FILE%
    type "%CFG_FILE%"
) else (
    echo   [INFO] No se encontró config.json externo; se usarán los valores por defecto.
)
echo.
set /p "RESP3=¿Deseas continuar al PASO 4 (Arrancar Centro de Control)? [S/N, Enter=S]: "
if /i "%RESP3%"=="N" goto CANCELADO


rem --- PASO 4: ARRANCAR CENTRO DE CONTROL ---
echo.
echo --------------------------------------------------------------------------------
echo [PASO 4/5] Lanzamiento del Centro de Control
echo --------------------------------------------------------------------------------
if "%EXE_TO_RUN%"=="" (
    echo [ERROR] No hay ejecutable disponible para lanzar.
    pause
    exit /b 1
)
echo   Lanzando proceso: "%EXE_TO_RUN%" --start-all
start "" "%EXE_TO_RUN%" --start-all
echo   [OK] Comando enviado. Esperando inicialización (4 segundos)...
timeout /t 4 /nobreak > nul

rem --- PASO 5: VERIFICAR PROCESOS EN EJECUCIÓN ---
echo.
echo --------------------------------------------------------------------------------
echo [PASO 5/5] Estado de Procesos en Tiempo Real
echo --------------------------------------------------------------------------------
powershell -NoProfile -Command "$procs = @('ReprediTrayDaemon', 'postgrest', 'caddy', 'sincronizador'); foreach ($pr in $procs) { $found = Get-Process -Name $pr -ErrorAction SilentlyContinue; if ($found) { Write-Host ('  [ACTIVO] ' + $pr + ' (PID: ' + ($found.Id -join ', ') + ')') -ForegroundColor Green } else { Write-Host ('  [DETENIDO / STANDBY] ' + $pr) -ForegroundColor Yellow } }"

echo.
echo ================================================================================
echo   [FIN] Diagnóstico y arranque paso a paso completado con éxito.
echo ================================================================================
echo.
pause
exit /b 0

:CANCELADO
echo.
echo   [INFO] Proceso cancelado por el usuario. No se iniciaron nuevos servicios.
echo.
pause
exit /b 0
