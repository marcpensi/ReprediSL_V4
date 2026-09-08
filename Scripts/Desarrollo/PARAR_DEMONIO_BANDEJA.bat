@echo off
chcp 65001 > nul
title Detener Demonio de Bandeja - ReprediSL V4 (.NET 10)
echo =========================================================
echo  HERRAMIENTA DE DETENCION DE EMERGENCIA DEL DEMONIO
echo =========================================================
echo.

taskkill /F /IM ReprediTrayDaemon.exe > nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Utilidades\PararDemonioBarraTareas.ps1" > nul 2>&1

echo.
echo Demonio (.NET 10) detenido y memoria liberada correctamente.
echo.
ping -n 3 127.0.0.1 > nul
