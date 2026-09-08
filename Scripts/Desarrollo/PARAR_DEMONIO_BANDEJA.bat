@echo off
chcp 65001 > nul
title Detener Demonio de Bandeja - ReprediSL V4
echo =========================================================
echo  HERRAMIENTA DE DETENCION DE EMERGENCIA DEL DEMONIO
echo =========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Utilidades\PararDemonioBarraTareas.ps1"

echo.
echo Presione cualquier tecla para cerrar esta ventana...
pause > nul
