@echo off
chcp 65001 > nul
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo [ERROR] Por favor, ejecuta este fichero COMO ADMINISTRADOR.
  pause
  exit /b 1
)

echo ================================================
echo  CONFIGURANDO FIREWALL DE WINDOWS (80 Y 443)
echo ================================================
echo.

netsh advfirewall firewall add rule name="REPREDISL API HTTP 80" dir=in action=allow protocol=TCP localport=80
netsh advfirewall firewall add rule name="REPREDISL API HTTPS 443" dir=in action=allow protocol=TCP localport=443

echo.
echo [OK] Puertos 80 (HTTP) y 443 (HTTPS) permitidos en el Firewall.
echo [INFO] NO es necesario publicar el puerto 3000 en Internet (solo Caddy lo usa en local).
pause
