@echo off
chcp 65001 > nul
title Demonio de Bandeja de Sistema - ReprediSL V4
echo =========================================================
echo  INICIANDO DEMONIO DE SINCRONIZACION EN BARRA DE TAREAS
echo =========================================================
echo.

start "" powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0..\Utilidades\DemonioBarraTareas.ps1"

echo Demonio iniciado correctamente.
echo Se ha abierto la ventana de monitoreo y el icono en la barra de tareas (junto al reloj).
echo.
timeout /t 3 > nul
