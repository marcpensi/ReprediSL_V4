@echo off
setlocal EnableExtensions EnableDelayedExpansion
title PsForce - Iniciar Sync

rem ============================================================
rem INICIAR_SYNC.bat
rem ARRANQUE COMPLETO DE PSFORCE
rem
rem 1. PostgreSQL 17
rem 2. PostgREST
rem 3. Caddy
rem 4. Sincronizador
rem 5. Programa principal / Centro de Control
rem ============================================================

set "ROOT=C:\Pensi\PsForce"

rem ---------- PostgreSQL ----------
set "PG_BIN=C:\Program Files\PostgreSQL\17\bin"
set "PG_SERVICE=postgresql-x64-17"
set "PGHOST=localhost"
set "PGPORT=5433"
set "PGUSER=postgres"
set "PGDATABASE=repredisl_api"
set "PGCLIENTENCODING=UTF8"

rem ---------- PostgREST ----------
set "POSTGREST_DIR=%ROOT%\ReprediTrayDaemon"
set "POSTGREST_EXE=%POSTGREST_DIR%\postgrest.exe"
set "POSTGREST_CONF=%POSTGREST_DIR%\postgrest.conf"
set "POSTGREST_PORT=3000"

rem ---------- Caddy ----------
set "CADDY_DIR=%ROOT%\Caddy"
set "CADDY_EXE=%CADDY_DIR%\caddy.exe"
set "CADDY_CONF=%CADDY_DIR%\Caddyfile"

rem ---------- Sync ----------
set "SYNC_DIR=%ROOT%\Sync"
set "SYNC_EXE=%SYNC_DIR%\sincronizador.exe"

rem ---------- Programa principal ----------
set "MAIN_EXE=%ROOT%\Daemon\ReprediTrayDaemon.exe"
if not exist "%MAIN_EXE%" set "MAIN_EXE=%ROOT%\ReprediTrayDaemon\ReprediTrayDaemon.exe"

echo ============================================================
echo   PSFORCE - INICIO COMPLETO
echo ============================================================
echo.

rem ------------------------------------------------------------
rem 0. PATH PostgreSQL / libpq.dll
rem ------------------------------------------------------------
if exist "%PG_BIN%\libpq.dll" (
    set "PATH=%PG_BIN%;%PATH%"
    echo [OK] PostgreSQL BIN agregado al PATH.
) else (
    echo [ERROR] No se encuentra %PG_BIN%\libpq.dll
    goto :ERROR
)

rem ------------------------------------------------------------
rem 0.1 Credencial persistente PGREPREAPIPWD
rem ------------------------------------------------------------
echo [INFO] Cargando PGREPREAPIPWD...

if not defined PGREPREAPIPWD (
    for /f "tokens=2,*" %%A in ('reg query "HKCU\Environment" /v PGREPREAPIPWD 2^>nul ^| findstr /I "PGREPREAPIPWD"') do set "PGREPREAPIPWD=%%B"
)

if not defined PGREPREAPIPWD (
    for /f "tokens=2,*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PGREPREAPIPWD 2^>nul ^| findstr /I "PGREPREAPIPWD"') do set "PGREPREAPIPWD=%%B"
)

if not defined PGREPREAPIPWD (
    echo [ERROR] No se encuentra PGREPREAPIPWD.
    goto :ERROR
)

rem Solo para que psql/libpq pueda autenticarse.
set "PGPASSWORD=%PGREPREAPIPWD%"
echo [OK] Credencial cargada.

rem ------------------------------------------------------------
rem 1. PostgreSQL
rem ------------------------------------------------------------
echo.
echo [1/5] PostgreSQL 17...

sc query "%PG_SERVICE%" >nul 2>&1
if errorlevel 1 (
    echo [ERROR] No existe el servicio %PG_SERVICE%
    goto :ERROR
)

sc query "%PG_SERVICE%" | findstr /I "RUNNING" >nul 2>&1
if errorlevel 1 (
    net start "%PG_SERVICE%" >nul 2>&1
    if errorlevel 1 (
        echo [ERROR] No se pudo iniciar PostgreSQL.
        echo         Ejecuta este BAT como administrador.
        goto :ERROR
    )
)

call :WAIT_PORT %PGPORT% 30
if errorlevel 1 (
    echo [ERROR] PostgreSQL no escucha en %PGPORT%.
    goto :ERROR
)

"%PG_BIN%\psql.exe" -X -w -v ON_ERROR_STOP=1 ^
  -h "%PGHOST%" ^
  -p "%PGPORT%" ^
  -U "%PGUSER%" ^
  -d "%PGDATABASE%" ^
  -tAc "SELECT 1;" >nul 2>&1

if errorlevel 1 (
    echo [ERROR] PostgreSQL responde, pero falla la autenticacion.
    echo         Revisa PGREPREAPIPWD.
    goto :ERROR
)

echo [OK] PostgreSQL listo en %PGHOST%:%PGPORT%.

rem ------------------------------------------------------------
rem 2. PostgREST
rem ------------------------------------------------------------
echo.
echo [2/5] PostgREST...

if not exist "%POSTGREST_EXE%" (
    echo [ERROR] No existe %POSTGREST_EXE%
    goto :ERROR
)

if not exist "%POSTGREST_CONF%" (
    echo [ERROR] No existe %POSTGREST_CONF%
    goto :ERROR
)

call :PORT_OPEN %POSTGREST_PORT%
if errorlevel 1 (
    start "PsForce PostgREST" /D "%POSTGREST_DIR%" "%POSTGREST_EXE%" "%POSTGREST_CONF%"
)

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
echo [3/5] Caddy...

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
    start "PsForce Caddy" /D "%CADDY_DIR%" "%CADDY_EXE%" run --config "%CADDY_CONF%"
    timeout /t 2 /nobreak >nul
)

set "CADDY_OK=0"
for /L %%I in (1,1,20) do (
    call :PORT_OPEN 80
    if not errorlevel 1 set "CADDY_OK=1"
    call :PORT_OPEN 443
    if not errorlevel 1 set "CADDY_OK=1"
    if "!CADDY_OK!"=="1" goto :CADDY_READY
    timeout /t 1 /nobreak >nul
)

echo [ERROR] Caddy no escucha en 80 ni 443.
goto :ERROR

:CADDY_READY
echo [OK] Caddy listo.

rem ------------------------------------------------------------
rem 4. Sincronizador
rem ------------------------------------------------------------
echo.
echo [4/5] Sincronizador...

if not exist "%SYNC_EXE%" (
    echo [ERROR] No existe %SYNC_EXE%
    goto :ERROR
)

tasklist /FI "IMAGENAME eq sincronizador.exe" 2>nul | find /I "sincronizador.exe" >nul
if errorlevel 1 (
    start "PsForce Sync" /D "%SYNC_DIR%" "%SYNC_EXE%"
    timeout /t 3 /nobreak >nul
)

tasklist /FI "IMAGENAME eq sincronizador.exe" 2>nul | find /I "sincronizador.exe" >nul
if errorlevel 1 (
    echo [ERROR] El sincronizador no permanece en ejecucion.
    goto :ERROR
)

echo [OK] Sincronizador ejecutandose.

rem ------------------------------------------------------------
rem 5. PROGRAMA PRINCIPAL / CENTRO DE CONTROL
rem ------------------------------------------------------------
echo.
echo [5/5] Programa principal PsForce...

if not exist "%MAIN_EXE%" (
    echo [ERROR] No encuentro ReprediTrayDaemon.exe en:
    echo         %ROOT%\Daemon
    echo         %ROOT%\ReprediTrayDaemon
    goto :ERROR
)

tasklist /FI "IMAGENAME eq ReprediTrayDaemon.exe" 2>nul | find /I "ReprediTrayDaemon.exe" >nul
if errorlevel 1 (
    echo       Abriendo Centro de Control...
    start "PsForce" "%MAIN_EXE%"
    timeout /t 2 /nobreak >nul
) else (
    echo       El Centro de Control ya estaba abierto.
)

echo [OK] Programa principal abierto.

echo.
echo ============================================================
echo   PSFORCE INICIADO CORRECTAMENTE
echo ============================================================
echo PostgreSQL : %PGHOST%:%PGPORT%/%PGDATABASE%
echo PostgREST  : 127.0.0.1:%POSTGREST_PORT%
echo Caddy      : 80 / 443
echo Sync       : ejecutandose
echo Principal  : ReprediTrayDaemon.exe
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
echo.
pause
exit /b 1
