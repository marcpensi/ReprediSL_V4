\
    @echo off
    title REPREDISL - Build Produccion
    cd /d "%~dp0"

    echo ================================================
    echo REPREDISL - GENERAR VERSION PARA HOSTINGER
    echo API: https://api.repredisl.com
    echo ================================================
    echo.

    if not exist node_modules (
      echo Instalando dependencias...
      call npm install
      if errorlevel 1 goto :error
    )

    echo.
    echo Generando carpeta dist...
    call npm run build
    if errorlevel 1 goto :error

    echo.
    echo ================================================
    echo CORRECTO
    echo La carpeta que debes publicar es:
    echo %CD%\dist
    echo ================================================
    echo.
    pause
    exit /b 0

    :error
    echo.
    echo ERROR: No se ha podido generar la version.
    echo Comprueba que Node.js y npm esten instalados.
    pause
    exit /b 1
