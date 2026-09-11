@echo off
setlocal EnableExtensions EnableDelayedExpansion
title Limpieza segura PsForce

rem ============================================================
rem  LIMPIAR_PsForce.bat
rem  - Por defecto SOLO SIMULA.
rem  - Para borrar de verdad: LIMPIAR_PsForce.bat /APLICAR
rem  - Conserva todos los componentes de ejecucion.
rem  - Si detecta una referencia a una carpeta candidata en los
rem    ficheros principales de arranque/configuracion, NO la borra.
rem ============================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "MODO=SIMULAR"
if /I "%~1"=="/APLICAR" set "MODO=APLICAR"

set "LOG=%ROOT%\LIMPIEZA_PsForce.log"

echo ============================================================
echo   PSFORCE - LIMPIEZA SEGURA
echo ============================================================
echo.
echo Carpeta: %ROOT%
echo Modo:    %MODO%
echo.

if /I not "%ROOT%"=="C:\Pensi\PsForce" (
    echo [AVISO] Este BAT esta pensado para C:\Pensi\PsForce
    echo         Se ha ejecutado desde: %ROOT%
    echo.
    choice /C SN /N /M "Continuar igualmente? [S/N]: "
    if errorlevel 2 goto :FIN
)

rem ------------------------------------------------------------
rem Verificacion minima: si falta algo esencial, ABORTAR.
rem ------------------------------------------------------------
set "ERROR=0"

call :REQUERIDO "%ROOT%\INICIAR_TODO.bat"
call :REQUERIDO "%ROOT%\DETENER_TODO.bat"
call :REQUERIDO "%ROOT%\Daemon\ReprediTrayDaemon.exe"
call :REQUERIDO "%ROOT%\Caddy\caddy.exe"
call :REQUERIDO "%ROOT%\Sync\sincronizador.exe"
call :REQUERIDO "%ROOT%\Config\psforce.ini"

if "%ERROR%"=="1" (
    echo.
    echo [ABORTADO] Falta algun componente esencial.
    echo No se ha borrado nada.
    goto :FIN
)

echo [OK] Componentes esenciales encontrados.
echo.

if /I "%MODO%"=="SIMULAR" (
    echo *** MODO SIMULACION ***
    echo No se borrara nada.
    echo Para aplicar la limpieza:
    echo.
    echo     LIMPIAR_PsForce.bat /APLICAR
    echo.
) else (
    echo *** MODO BORRADO REAL ***
    echo.
    choice /C SN /N /M "Confirmas la limpieza? [S/N]: "
    if errorlevel 2 goto :FIN
)

echo ------------------------------------------------------------ >> "%LOG%"
echo %DATE% %TIME% - Modo %MODO% >> "%LOG%"

rem ============================================================
rem 1. ARCHIVOS CLARAMENTE TEMPORALES / COPIAS
rem ============================================================

call :BORRAR_ARCHIVO "%ROOT%\API.rar"
call :BORRAR_ARCHIVO "%ROOT%\dist_cliente.rar"
call :BORRAR_ARCHIVO "%ROOT%\Scripts.rar"
call :BORRAR_ARCHIVO "%ROOT%\PsForceInstalador.zip"
call :BORRAR_ARCHIVO "%ROOT%\edb_psqlodbc.exe-20260911143324"
call :BORRAR_ARCHIVO "%ROOT%\config.json.txt"

rem ============================================================
rem 2. CARPETAS NO NECESARIAS EN EJECUCION
rem    Solo se eliminan si NO aparecen referenciadas en los
rem    ficheros principales de arranque/configuracion.
rem ============================================================

call :BORRAR_DIR_SI_NO_REFERENCIADA "src"
call :BORRAR_DIR_SI_NO_REFERENCIADA "dist_cliente"
call :BORRAR_DIR_SI_NO_REFERENCIADA "PsForce_Instalador"

rem ============================================================
rem 3. NO TOCAR
rem ============================================================
echo.
echo Se CONSERVAN expresamente:
echo   API
echo   Backup
echo   Caddy
echo   Config
echo   Daemon
echo   Logs
echo   ReprediTrayDaemon
echo   Scripts
echo   Sync
echo   INICIAR_TODO.bat / DETENER_TODO.bat
echo   ARRANCAR_TODO.bat / PARAR_TODO.bat
echo.

if /I "%MODO%"=="SIMULAR" (
    echo [FIN] Simulacion terminada. No se ha borrado nada.
    echo Ejecuta:
    echo     "%~nx0" /APLICAR
    echo cuando quieras realizar la limpieza.
) else (
    echo [FIN] Limpieza terminada.
    echo Registro: "%LOG%"
)

goto :FIN

:REQUERIDO
if not exist "%~1" (
    echo [FALTA] %~1
    set "ERROR=1"
) else (
    echo [OK]    %~1
)
exit /b

:BORRAR_ARCHIVO
if not exist "%~1" exit /b
if /I "%MODO%"=="SIMULAR" (
    echo [BORRARIA] Archivo: %~1
    echo [BORRARIA] Archivo: %~1 >> "%LOG%"
) else (
    del /F /Q "%~1" >nul 2>&1
    if exist "%~1" (
        echo [ERROR] No se pudo borrar: %~1
        echo [ERROR] No se pudo borrar: %~1 >> "%LOG%"
    ) else (
        echo [BORRADO] %~1
        echo [BORRADO] %~1 >> "%LOG%"
    )
)
exit /b

:BORRAR_DIR_SI_NO_REFERENCIADA
set "NOMBRE=%~1"
set "DIR=%ROOT%\%NOMBRE%"
if not exist "%DIR%\" exit /b

set "REFERENCIADA=0"

for %%F in (
    "%ROOT%\INICIAR_TODO.bat"
    "%ROOT%\DETENER_TODO.bat"
    "%ROOT%\ARRANCAR_TODO.bat"
    "%ROOT%\PARAR_TODO.bat"
    "%ROOT%\Config\psforce.ini"
) do (
    if exist "%%~F" (
        findstr /I /C:"%NOMBRE%" "%%~F" >nul 2>&1
        if not errorlevel 1 set "REFERENCIADA=1"
    )
)

if "!REFERENCIADA!"=="1" (
    echo [CONSERVADO] "%DIR%" - aparece referenciada.
    echo [CONSERVADO] "%DIR%" - aparece referenciada. >> "%LOG%"
    exit /b
)

if /I "%MODO%"=="SIMULAR" (
    echo [BORRARIA] Carpeta: "%DIR%"
    echo [BORRARIA] Carpeta: "%DIR%" >> "%LOG%"
) else (
    rd /S /Q "%DIR%" >nul 2>&1
    if exist "%DIR%\" (
        echo [ERROR] No se pudo borrar: "%DIR%"
        echo [ERROR] No se pudo borrar: "%DIR%" >> "%LOG%"
    ) else (
        echo [BORRADA] "%DIR%"
        echo [BORRADA] "%DIR%" >> "%LOG%"
    )
)
exit /b

:FIN
echo.
pause
endlocal
