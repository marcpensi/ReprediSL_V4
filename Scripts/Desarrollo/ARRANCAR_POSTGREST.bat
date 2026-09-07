\
    @echo off
    title REPREDISL API - PostgREST
    cd /d "%~dp0"

    echo ================================================
    echo REPREDISL API - PostgREST
    echo ================================================
    echo.
    postgrest.exe postgrest.conf
    echo.
    echo PostgREST se ha detenido.
    pause
