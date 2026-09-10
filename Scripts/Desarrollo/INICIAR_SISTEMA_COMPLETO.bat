@echo off
chcp 65001 > nul
title ReprediSL V4 - Iniciar Todos los Servicios
echo =========================================================
echo  REPREDISL V4 - INICIO COMPLETO DE SERVICIOS
echo =========================================================
echo.
echo 1. Iniciando API PostgREST (Puerto 3000)...
start "ReprediSL API - PostgREST" "%~dp0ARRANCAR_POSTGREST.bat"
ping -n 3 127.0.0.1 > nul

echo 2. Iniciando Caddy Reverse Proxy (HTTPS 443)...
start "ReprediSL Proxy - Caddy" "%~dp0ARRANCAR_CADDY.bat"
ping -n 3 127.0.0.1 > nul

echo 3. Iniciando Sincronizador de Pedidos a Access ERP...
start "ReprediSL Sync - Pedidos Access" "%~dp0ARRANCAR_SYNC_PEDIDOS.bat"
ping -n 3 127.0.0.1 > nul

echo 4. Iniciando Demonio de Notificaciones en Bandeja...
start "" "%~dp0..\..\src\Daemon\bin\ReprediTrayDaemon.exe"
ping -n 2 127.0.0.1 > nul

echo.
echo =========================================================
echo  ¡TODOS LOS SERVICIOS ESTAN EN EJECUCION!
echo =========================================================
echo  - API Publica:       https://api.repredisl.com
echo  - App Comercial PWA: https://pedidos.repredisl.com
echo  - Base Datos ERP:    C:\pensi\psgestw\e0012026\gestion.mdb
echo  - Bandeja Sistema:   Icono activo junto al reloj de Windows
echo =========================================================
echo.
ping -n 4 127.0.0.1 > nul
