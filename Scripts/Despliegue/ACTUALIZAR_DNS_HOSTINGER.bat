@echo off
chcp 65001 > nul
title Actualizar DNS api.repredisl.com - Hostinger MCP
cd /d "%~dp0"

echo =======================================================
echo  ACTUALIZAR DNS HOSTINGER (api.repredisl.com)
echo =======================================================
echo.
echo Detectando IP pública y actualizando registro A en Hostinger...
echo.

node "%~dp0ActualizarDnsApiHostinger.mjs"

if %errorlevel% equ 0 (
    echo.
    echo [OK] Registro DNS actualizado correctamente.
) else (
    echo.
    echo [ERROR] Hubo un problema al actualizar el registro DNS.
)

echo.
pause
