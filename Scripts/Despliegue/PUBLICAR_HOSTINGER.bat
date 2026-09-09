@echo off
chcp 65001 > nul
title Publicador Hostinger MCP - ReprediSL V4
echo ========================================================
echo  PUBLICANDO PWA EN HOSTINGER (pedidos.repredisl.com)
echo ========================================================
echo.

node "%~dp0PublicarHostingerMcp.mjs"

if errorlevel 1 (
  echo.
  echo [ERROR] Hubo un problema durante la publicación.
  pause
  exit /b 1
)

echo.
echo ========================================================
echo  DESPLIEGUE FINALIZADO CON ÉXITO
echo  URL: https://pedidos.repredisl.com
echo ========================================================
pause
