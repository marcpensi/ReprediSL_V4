\
    @echo off
    net session >nul 2>&1
    if %errorlevel% neq 0 (
      echo Ejecuta este fichero COMO ADMINISTRADOR.
      pause
      exit /b 1
    )

    netsh advfirewall firewall add rule name="REPREDISL API HTTP 80" dir=in action=allow protocol=TCP localport=80
    netsh advfirewall firewall add rule name="REPREDISL API HTTPS 443" dir=in action=allow protocol=TCP localport=443

    echo.
    echo Puertos 80 y 443 permitidos.
    echo NO es necesario publicar el puerto 3000 en Internet.
    pause
