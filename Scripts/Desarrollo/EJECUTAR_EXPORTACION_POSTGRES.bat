@echo off
chcp 65001 > nul
title Exportador Access a PostgreSQL - ReprediSL V4
echo =========================================================
echo  EJECUTANDO EXPORTACION MDB A POSTGRESQL DESDE FUERA
echo =========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\BaseDatos\EjecutarExportacionAccess.ps1"

echo.
echo Presiona cualquier tecla para salir...
pause > nul
