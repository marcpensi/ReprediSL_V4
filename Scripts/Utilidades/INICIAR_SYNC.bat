@echo off
setlocal EnableExtensions EnableDelayedExpansion
title PsForce - Iniciar Sync

rem ============================================================
rem INICIAR_SYNC.bat
rem Arranca PsForce en orden y espera a que cada componente
rem ESTE LISTO antes de continuar.
rem
rem Orden:
rem   1. PostgreSQL 17 (servicio / puerto 5433)
rem   2. PostgREST (puerto 3000)
rem   3. Caddy (puertos 80/443)
rem   4. Sincronizador
rem ============================================================

set "ROOT=C:\Pensi\PsForce"
set "PG_BIN=C:\Program Files\PostgreSQL\17\bin"
set "PG_SERVICE=postgresql-x64-17"
set "PG_PORT=5433"

set "POSTGREST_DIR=%ROOT%\ReprediTrayDaemon"
set "POSTGREST_EXE=%POSTGREST_DIR%\postgrest.exe"
set "POSTGREST_CONF=%POSTGREST_DIR%\postgrest.conf"
set "POSTGREST_PORT=3000"

set "CADDY_DIR=%ROOT%\Caddy"
set "CADDY_EXE=%CADDY_DIR%\caddy.exe"
set "CADDY_CONF=%CADDY_DIR%\Caddyfile"

set "SYNC_DIR=%ROOT%\Sync"
set "SYNC_EXE=%SYNC_DIR%\sincronizador.exe"

echo ============================================================
echo   PSFORCE - INICIO SECUENCIAL DE SINCRONIZACION
echo ============================================================
echo.

rem ------------------------------------------------------------
rem 0. PATH PostgreSQL / libpq.dll
rem ------------------------------------------------------------
if exist "%PG_BIN%\libpq.dll" (
    set "PATH=%PG_BIN%;%PATH%"
    echo [OK] PostgreSQL BIN agregado al PATH de esta sesion.
) else (
    echo [ERROR] No se encuentra:
    echo         %PG_BIN%\libpq.dll
    goto :ERROR
)

rem ------------------------------------------------------------
rem 1. PostgreSQL
rem ------------------------------------------------------------
echo.
echo [1/4] PostgreSQL 17...

sc query "%PG_SERVICE%" >nul 2>&1
if errorlevel 1 (
    echo [ERROR] No existe el servicio %PG_SERVICE%
    echo Servicios PostgreSQL detectados:
    sc query type^= service state^= all | findstr /I "postgres"
    goto :ERROR
)

sc query "%PG_SERVICE%" | findstr /I "RUNNING" >nul 2>&1
if errorlevel 1 (
    echo       Arrancando servicio %PG_SERVICE%...
    net start "%PG_SERVICE%" >nul 2>&1
    if errorlevel 1 (
        echo [ERROR] No se pudo iniciar PostgreSQL.
        echo         Ejecuta este BAT como administrador.
        goto :ERROR
    )
) else (
    echo       El servicio ya estaba iniciado.
)

echo       Esperando puerto %PG_PORT%...
call :WAIT_PORT %PG_PORT% 30
if errorlevel 1 (
    echo [ERROR] PostgreSQL no escucha en el puerto %PG_PORT%.
    goto :ERROR
)
echo [OK] PostgreSQL listo en %PG_PORT%.

rem ------------------------------------------------------------
rem 2. PostgREST
rem ------------------------------------------------------------
echo.
echo [2/4] PostgREST...

if not exist "%POSTGREST_EXE%" (
    echo [ERROR] No existe %POSTGREST_EXE%
    goto :ERROR
)
if not exist "%POSTGREST_CONF%" (
    echo [ERROR] No existe %POSTGREST_CONF%
    goto :ERROR
)

call :PORT_OPEN %POSTGREST_PORT%
if not errorlevel 1 (
    echo       PostgREST ya estaba escuchando en %POSTGREST_PORT%.
) else (
    echo       Iniciando PostgREST...
    start "PsForce PostgREST" /D "%POSTGREST_DIR%" "%POSTGREST_EXE%" "%POSTGREST_CONF%"
)

echo       Esperando puerto %POSTGREST_PORT%...
call :WAIT_PORT %POSTGREST_PORT% 30
if errorlevel 1 (
    echo [ERROR] PostgREST no ha quedado listo en %POSTGREST_PORT%.
    goto :ERROR
)
echo [OK] PostgREST listo en %POSTGREST_PORT%.

rem ------------------------------------------------------------
rem 3. Caddy
rem ------------------------------------------------------------
echo.
echo [3/4] Caddy...

if not exist "%CADDY_EXE%" (
    echo [ERROR] No existe %CADDY_EXE%
    goto :ERROR
)
if not exist "%CADDY_CONF%" (
    echo [ERROR] No existe %CADDY_CONF%
    goto :ERROR
)

tasklist /FI "IMAGENAME eq caddy.exe" 2>nul | find /I "caddy.exe" >nul
if errorlevel 1 (
    echo       Iniciando Caddy...
    start "PsForce Caddy" /D "%CADDY_DIR%" "%CADDY_EXE%" run --config "%CADDY_CONF%"
    timeout /t 2 /nobreak >nul
) else (
    echo       Caddy ya estaba iniciado.
)

rem Caddy puede escuchar en 80, 443 o ambos.
echo       Esperando Caddy...
set "CADDY_OK=0"
for /L %%I in (1,1,20) do (
    call :PORT_OPEN 80
    if not errorlevel 1 set "CADDY_OK=1"
    call :PORT_OPEN 443
    if not errorlevel 1 set "CADDY_OK=1"
    if "!CADDY_OK!"=="1" goto :CADDY_READY
    timeout /t 1 /nobreak >nul
)

echo [ERROR] Caddy no esta escuchando en 80 ni 443.
goto :ERROR

:CADDY_READY
echo [OK] Caddy listo.

rem ------------------------------------------------------------
rem 4. Sincronizador
rem ------------------------------------------------------------
echo.
echo [4/4] Sincronizador...

if not exist "%SYNC_EXE%" (
    echo [ERROR] No existe %SYNC_EXE%
    goto :ERROR
)

tasklist /FI "IMAGENAME eq sincronizador.exe" 2>nul | find /I "sincronizador.exe" >nul
if errorlevel 1 (
    echo       Iniciando sincronizador...
    start "PsForce Sync" /D "%SYNC_DIR%" "%SYNC_EXE%"
    timeout /t 2 /nobreak >nul
) else (
    echo       El sincronizador ya estaba iniciado.
)

tasklist /FI "IMAGENAME eq sincronizador.exe" 2>nul | find /I "sincronizador.exe" >nul
if errorlevel 1 (
    echo [ERROR] El sincronizador no permanece en ejecucion.
    goto :ERROR
)

echo [OK] Sincronizador iniciado.

echo.
echo ============================================================
echo   TODO INICIADO CORRECTAMENTE
echo ============================================================
echo PostgreSQL : localhost:%PG_PORT%
echo PostgREST  : 127.0.0.1:%POSTGREST_PORT%
echo Caddy      : 80 / 443
echo Sync       : ejecutandose
echo ============================================================
echo.
pause
exit /b 0

rem ============================================================
rem FUNCIONES
rem ============================================================

:PORT_OPEN
powershell.exe -NoProfile -Command ^
  "$c = Get-NetTCPConnection -State Listen -LocalPort %1 -ErrorAction SilentlyContinue; if($c){exit 0}else{exit 1}" >nul 2>&1
exit /b %errorlevel%

:WAIT_PORT
set "WP_PORT=%~1"
set "WP_SECONDS=%~2"
for /L %%I in (1,1,%WP_SECONDS%) do (
    call :PORT_OPEN %WP_PORT%
    if not errorlevel 1 exit /b 0
    timeout /t 1 /nobreak >nul
)
exit /b 1

:ERROR
echo.
echo ============================================================
echo   INICIO DETENIDO POR ERROR
echo ============================================================
echo No se inicia el siguiente componente hasta resolver el actual.
echo.
pause
exit /b 1
