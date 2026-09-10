@echo off
chcp 65001 > nul
title REPREDISL - Caddy Reverse Proxy (HTTPS -> PostgREST)

echo =========================================================
echo  REPREDISL API - CADDY REVERSE PROXY (HTTPS 443)
echo =========================================================
echo.
echo  Dominio publico:   https://api.repredisl.com
echo  Destino interno:   http://127.0.0.1:3000 (PostgREST)
echo  Certificado SSL:   Automatico con Let's Encrypt (Caddy)
echo.

set "CADDY_BIN=caddy"
where caddy > nul 2>&1
if %errorlevel% neq 0 (
    if exist "%USERPROFILE%\scoop\shims\caddy.exe" (
        set "CADDY_BIN=%USERPROFILE%\scoop\shims\caddy.exe"
    ) else (
        echo [ERROR] No se ha encontrado el ejecutable de Caddy en PATH ni en scoop.
        pause
        exit /b 1
    )
)

set "CADDYFILE="
if exist "%~dp0..\..\src\API\Caddyfile" set "CADDYFILE=%~dp0..\..\src\API\Caddyfile"
if exist "%~dp0Caddyfile" set "CADDYFILE=%~dp0Caddyfile"

if "%CADDYFILE%"=="" (
    echo [ERROR] No se ha encontrado Caddyfile.
    pause
    exit /b 1
)

echo Usando ejecutable:    %CADDY_BIN%
echo Usando configuracion: %CADDYFILE%
echo.
echo Comprobando si Caddy ya esta en ejecucion...
tasklist /FI "IMAGENAME eq caddy.exe" 2>NUL | find /I /N "caddy.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [INFO] Caddy ya esta ejecutandose en segundo plano (puertos 80 y 443 activos).
    echo Si deseas reiniciar Caddy, ejecuta primero PARAR_CADDY.bat.
    echo ---------------------------------------------------------
    pause
    exit /b 0
)

echo Iniciando proxy inverso en primer plano...
echo Presiona Ctrl + C para detener el servidor Caddy.
echo ---------------------------------------------------------

"%CADDY_BIN%" run --config "%CADDYFILE%"

echo.
echo Caddy se ha detenido.
pause
