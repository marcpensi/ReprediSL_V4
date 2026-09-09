@echo off
chcp 65001 > nul
title REPREDISL API - PostgREST (127.0.0.1:3000)
cd /d "%~dp0"

echo ================================================
echo  REPREDISL API - POSTGREST (PUERTO 3000)
echo ================================================
echo.

if not exist "%~dp0postgrest.exe" (
    echo [ERROR] No se encuentra postgrest.exe en %~dp0
    pause
    exit /b 1
)

echo Carpeta de trabajo: %CD%
echo Archivo de config:  postgrest.conf
echo.
echo Iniciando PostgREST...
echo Presiona Ctrl + C para detener el servicio.
echo ------------------------------------------------

postgrest.exe postgrest.conf

echo.
echo PostgREST se ha detenido.
pause
