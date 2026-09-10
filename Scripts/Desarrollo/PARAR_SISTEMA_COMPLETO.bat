@echo off
chcp 65001 > nul
title ReprediSL V4 - Detener Todos los Servicios
echo =========================================================
echo  REPREDISL V4 - DETENER TODOS LOS SERVICIOS
echo =========================================================
echo.
echo Deteniendo Demonio de Bandeja (ReprediTrayDaemon)...
taskkill /F /IM ReprediTrayDaemon.exe > nul 2>&1

echo Deteniendo Caddy Reverse Proxy...
taskkill /F /IM caddy.exe > nul 2>&1

echo Deteniendo API PostgREST...
taskkill /F /IM postgrest.exe > nul 2>&1

echo Deteniendo Sincronizador de Pedidos...
powershell -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*SincronizarPedidosEntrantes.ps1*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" > nul 2>&1

echo.
echo =========================================================
echo  [OK] Todos los servicios se han detenido correctamente.
echo =========================================================
ping -n 3 127.0.0.1 > nul
