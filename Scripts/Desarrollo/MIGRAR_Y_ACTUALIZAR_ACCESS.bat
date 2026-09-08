@echo off
chcp 65001 > nul
title Migrador y Actualizador de Microsoft Access - ReprediSL V4
echo =========================================================
echo  EJECUTANDO ACTUALIZADOR AUTOMATICO DE MICROSOFT ACCESS
echo =========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Migracion\MigrarYActualizarAccess.ps1"

echo.
echo Presiona cualquier tecla para salir...
pause > nul
