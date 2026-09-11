@echo off
chcp 65001 > nul
title ReprediSL V4 - Centro de Control y Servicios
echo =========================================================
echo   REPREDISL V4 - CENTRO DE CONTROL Y SERVICIOS
echo =========================================================
echo.
echo   Iniciando Centro de Control Unificado...
echo   Servicios gestionados:
echo     1. PostgreSQL
echo     2. PostgREST API
echo     3. Caddy Reverse Proxy
echo     4. Sincronizador de Pedidos (Access ERP)
echo.

set "DAEMON_EXE=%~dp0..\..\src\Daemon\bin\ReprediTrayDaemon.exe"
if not exist "%DAEMON_EXE%" set "DAEMON_EXE=%~dp0..\..\src\Daemon\ReprediTrayDaemon\bin\Release\net10.0-windows\ReprediTrayDaemon.exe"
if not exist "%DAEMON_EXE%" set "DAEMON_EXE=%~dp0..\..\ReprediTrayDaemon\ReprediTrayDaemon.exe"
if not exist "%DAEMON_EXE%" (
    echo [ERROR] No se encuentra ReprediTrayDaemon.exe
    echo Ejecuta: dotnet build "%~dp0..\..\src\Daemon\ReprediTrayDaemon\ReprediTrayDaemon.csproj"
    pause
    exit /b 1
)

start "" "%DAEMON_EXE%" --start-all

echo =========================================================
echo   [OK] Centro de Control iniciado.
echo   Ver estado y gestionar desde la ventana o la bandeja.
echo =========================================================
echo.
ping -n 3 127.0.0.1 > nul
