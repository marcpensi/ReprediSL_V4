@echo off
chcp 65001 > nul
title Demonio de Bandeja de Sistema - ReprediSL V4 (.NET 10)
echo =========================================================
echo  INICIANDO DEMONIO DE SINCRONIZACION NATIVO (.NET 10)
echo =========================================================
echo.

taskkill /F /IM ReprediTrayDaemon.exe > nul 2>&1
ping -n 2 127.0.0.1 > nul

start "" "%~dp0..\..\src\Daemon\bin\ReprediTrayDaemon.exe"

echo Demonio iniciado correctamente (.NET 10).
echo Se ha abierto la ventana de monitoreo e icono en la barra de tareas (junto al reloj).
echo.
ping -n 3 127.0.0.1 > nul
