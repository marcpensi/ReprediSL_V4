@echo off
chcp 65001 > nul
title REPREDISL - Caddy Reverse Proxy (HTTPS -> PostgREST)
cd /d "%~dp0"

echo =========================================================
echo  REPREDISL API - CADDY REVERSE PROXY (HTTPS 443)
echo =========================================================
echo.
echo  Dominio público:   https://api.repredisl.com
echo  Destino interno:   http://127.0.0.1:3000 (PostgREST)
echo  Certificado SSL:   Automático con Let's Encrypt (Caddy)
echo.

where caddy > nul 2>&1
if %errorlevel% neq 0 (
    if exist "%USERPROFILE%\scoop\shims\caddy.exe" (
        set "CADDY_BIN=%USERPROFILE%\scoop\shims\caddy.exe"
    ) else (
        echo [ERROR] No se ha encontrado el ejecutable de Caddy en PATH ni en scoop.
        pause
        exit /b 1
    )
) else (
    set "CADDY_BIN=caddy"
)

if not exist "%~dp0Caddyfile" (
    echo [ERROR] No se ha encontrado Caddyfile en esta carpeta.
    pause
    exit /b 1
)

echo Usando ejecutable:    %CADDY_BIN%
echo Usando configuración: %~dp0Caddyfile
echo.
echo Iniciando proxy inverso en primer plano...
echo Presiona Ctrl + C para detener el servidor Caddy.
echo ---------------------------------------------------------

"%CADDY_BIN%" run --config "%~dp0Caddyfile"

echo.
echo Caddy se ha detenido.
pause
