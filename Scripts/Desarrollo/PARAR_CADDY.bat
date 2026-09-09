@echo off
chcp 65001 > nul
title Parar Caddy Server - ReprediSL V4
echo ================================================
echo  DETENIENDO SERVIDOR CADDY REVERSE PROXY
echo ================================================
echo.

taskkill /F /IM caddy.exe > nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Proceso Caddy detenido correctamente.
) else (
    echo [INFO] No había ninguna instancia de Caddy en ejecución.
)

timeout /t 2 > nul
