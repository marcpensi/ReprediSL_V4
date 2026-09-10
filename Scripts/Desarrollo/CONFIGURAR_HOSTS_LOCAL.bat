@echo off
chcp 65001 > nul
title ReprediSL V4 - Configurar Hosts Local (api.repredisl.com)

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo =========================================================
    echo [ERROR] Este script requiere permisos de Administrador.
    echo Por favor, haz clic derecho y selecciona:
    echo "Ejecutar como administrador".
    echo =========================================================
    pause
    exit /b 1
)

echo =========================================================
echo  CONFIGURANDO RESOLUCION LOCAL PARA api.repredisl.com
echo =========================================================
echo.
echo Esto permite que el navegador en ESTE ordenador pueda
echo conectar directamente a Caddy HTTPS sin que el router
echo bloquee las peticiones locales (Loopback NAT).
echo.

findstr /i "api.repredisl.com" "%WINDIR%\System32\drivers\etc\hosts" >nul
if %errorlevel% equ 0 (
    echo [OK] api.repredisl.com ya está presente en el archivo hosts.
) else (
    echo 127.0.0.1 api.repredisl.com>> "%WINDIR%\System32\drivers\etc\hosts"
    echo [OK] Añadido '127.0.0.1 api.repredisl.com' a %WINDIR%\System32\drivers\etc\hosts.
)

echo.
echo Limpiando cache DNS de Windows...
ipconfig /flushdns >nul
echo [OK] Cache DNS limpia.
echo.
echo =========================================================
echo Listo. Ya puedes recargar https://pedidos.repredisl.com
echo =========================================================
pause
