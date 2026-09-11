<#
.SYNOPSIS
    Prepara el paquete de instalación oficial para el cliente en C:\pensi\psforce.
.DESCRIPTION
    Compila en Release ReprediTrayDaemon y el Sincronizador de pedidos (.exe),
    organiza la estructura exacta solicitada:
      C:\pensi\psforce\
      ├── ReprediTrayDaemon\ (exe, dlls, config.json, postgrest)
      ├── Sync\ (sincronizador.exe, config.json, script)
      ├── Caddy\ (caddy.exe, Caddyfile)
      ├── Logs\
      └── Backup\
    Genera tanto el directorio en C:\pensi\psforce como un paquete portátil en dist_cliente\ReprediSL.
#>

param (
    [string]$DestinoPrincipal = "C:\pensi\psforce",
    [switch]$NoPortable = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = (Get-Item (Join-Path $ScriptDir "..\..")).FullName

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  REPREDISL V4 - PREPARADOR DE INSTALACIÓN PARA CLIENTE                        " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "• Raíz de Proyecto: $ProjectRoot"
Write-Host "• Destino Cliente : $DestinoPrincipal"
Write-Host ""

# 1. Compilación y publicación de ReprediTrayDaemon
Write-Host "[1/6] Compilando y publicando ReprediTrayDaemon (Release)..." -ForegroundColor Yellow
$daemonProj = Join-Path $ProjectRoot "src\Daemon\ReprediTrayDaemon\ReprediTrayDaemon.csproj"
$tempPublishDaemon = Join-Path $ProjectRoot "scratch\publish_daemon"
if (Test-Path $tempPublishDaemon) { Remove-Item $tempPublishDaemon -Recurse -Force }

& dotnet publish $daemonProj -c Release -o $tempPublishDaemon --nologo
if ($LASTEXITCODE -ne 0) { throw "Error al publicar ReprediTrayDaemon." }

# 2. Compilación y publicación de ReprediSync (sincronizador.exe)
Write-Host "[2/6] Compilando y publicando ReprediSync (sincronizador.exe Release)..." -ForegroundColor Yellow
$syncProj = Join-Path $ProjectRoot "src\Sync\ReprediSync\ReprediSync.csproj"
$tempPublishSync = Join-Path $ProjectRoot "scratch\publish_sync"
if (Test-Path $tempPublishSync) { Remove-Item $tempPublishSync -Recurse -Force }

& dotnet publish $syncProj -c Release -o $tempPublishSync --nologo
if ($LASTEXITCODE -ne 0) { throw "Error al publicar ReprediSync." }

# 3. Localizar binario Caddy
Write-Host "[3/6] Localizando ejecutable de Caddy..." -ForegroundColor Yellow
$caddySource = ""
$caddyCandidates = @(
    (Get-ChildItem "$env:USERPROFILE\scoop\apps\caddy" -Recurse -Filter "caddy.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1),
    "$env:USERPROFILE\scoop\shims\caddy.exe",
    (Join-Path $ProjectRoot "src\API\caddy.exe")
)
foreach ($cand in $caddyCandidates) {
    if ($cand -and (Test-Path $cand)) {
        $caddySource = $cand
        break
    }
}
if (-not $caddySource) {
    Write-Warning "No se localizó caddy.exe en scoop. Se usará el del PATH o shim si existe."
} else {
    Write-Host "  -> Encontrado caddy en: $caddySource" -ForegroundColor Green
}

# 4. Crear estructura de carpetas
$rutasDestino = @($DestinoPrincipal)
if (-not $NoPortable) {
    $rutasDestino += (Join-Path $ProjectRoot "dist_cliente\ReprediSL")
}

foreach ($target in $rutasDestino) {
    Write-Host "[4/6] Desplegando estructura en $target..." -ForegroundColor Yellow
    
    $dirDaemon = Join-Path $target "ReprediTrayDaemon"
    $dirSync   = Join-Path $target "Sync"
    $dirCaddy  = Join-Path $target "Caddy"
    $dirLogs   = Join-Path $target "Logs"
    $dirBackup = Join-Path $target "Backup"

    @($target, $dirDaemon, $dirSync, $dirCaddy, $dirLogs, $dirBackup) | ForEach-Object {
        if (-not (Test-Path $_)) { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
    }

    # Copiar contenido de ReprediTrayDaemon
    Copy-Item "$tempPublishDaemon\*" $dirDaemon -Recurse -Force

    # Copiar postgrest.exe y postgrest.conf en ReprediTrayDaemon
    $postgrestExe = Join-Path $ProjectRoot "src\API\postgrest.exe"
    $postgrestConf = Join-Path $ProjectRoot "src\API\postgrest.conf"
    if (Test-Path $postgrestExe) { Copy-Item $postgrestExe $dirDaemon -Force }
    if (Test-Path $postgrestConf) { Copy-Item $postgrestConf $dirDaemon -Force }

    # Generar config.json de ReprediTrayDaemon
    $daemonConfigJson = @"
{
  "appName": "ReprediSL Centro de Control",
  "version": "4.9.2",
  "baseDir": "$($target.Replace('\', '\\'))",
  "accessMdbPath": "C:\\pensi\\psgestw\\e0012026\\gestion.mdb",
  "syncExecutable": "..\\Sync\\sincronizador.exe",
  "syncScript": "..\\Sync\\SincronizarPedidosEntrantes.ps1",
  "syncIntervalSeconds": 3,
  "caddyExecutable": "..\\Caddy\\caddy.exe",
  "caddyfile": "..\\Caddy\\Caddyfile",
  "postgrestExecutable": "postgrest.exe",
  "postgrestConfig": "postgrest.conf",
  "logsDir": "..\\Logs",
  "backupDir": "..\\Backup",
  "postgresHost": "127.0.0.1",
  "postgresPort": 5432,
  "postgresDatabase": "repredisl_api",
  "postgresUser": "postgres",
  "postgrestPort": 3000,
  "caddyPort": 80,
  "autoStartServices": true
}
"@
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText((Join-Path $dirDaemon "config.json"), $daemonConfigJson, $utf8Bom)

    # Copiar contenido de Sync
    Copy-Item "$tempPublishSync\*" $dirSync -Recurse -Force
    
    # Copiar script de sincronización a Sync
    $syncScriptSrc = Join-Path $ProjectRoot "Scripts\BaseDatos\SincronizarPedidosEntrantes.ps1"
    Copy-Item $syncScriptSrc $dirSync -Force

    # Generar config.json de Sync
    $syncConfigJson = @"
{
  "accessMdbPath": "C:\\pensi\\psgestw\\e0012026\\gestion.mdb",
  "logsDir": "..\\Logs",
  "scriptFile": "SincronizarPedidosEntrantes.ps1",
  "intervalSeconds": 3,
  "postgresHost": "127.0.0.1",
  "postgresPort": 5432,
  "postgresDatabase": "repredisl_api"
}
"@
    [System.IO.File]::WriteAllText((Join-Path $dirSync "config.json"), $syncConfigJson, $utf8Bom)

    # Script para arrancar sincronizador standalone
    $arrancarSyncBat = @"
@echo off
title ReprediSL - Sincronizador de Pedidos
cd /d "%~dp0"
sincronizador.exe -Loop -IntervaloSegundos 3
pause
"@
    [System.IO.File]::WriteAllText((Join-Path $dirSync "ARRANCAR_SINCRONIZADOR.bat"), $arrancarSyncBat, $utf8Bom)

    # Configurar Caddy
    if ($caddySource -and (Test-Path $caddySource)) {
        Copy-Item $caddySource (Join-Path $dirCaddy "caddy.exe") -Force
    }
    $caddyfileSrc = Join-Path $ProjectRoot "src\API\Caddyfile"
    Copy-Item $caddyfileSrc (Join-Path $dirCaddy "Caddyfile") -Force

    $arrancarCaddyBat = @"
@echo off
title ReprediSL - Caddy Reverse Proxy (SSL)
cd /d "%~dp0"
caddy.exe run --config Caddyfile
pause
"@
    [System.IO.File]::WriteAllText((Join-Path $dirCaddy "ARRANCAR_CADDY.bat"), $arrancarCaddyBat, $utf8Bom)

    # Helper script para Backup de Access
    $backupBat = @"
@echo off
chcp 65001 > nul
title ReprediSL - Copia de Seguridad Access
cd /d "%~dp0"
set "ORIGEN=C:\pensi\psgestw\e0012026\gestion.mdb"
set "DESTINO=%~dp0gestion_%date:~6,4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%.mdb"
set "DESTINO=%DESTINO: =0%"

echo Realizando copia de seguridad de %ORIGEN% ...
if not exist "%ORIGEN%" (
    echo [ERROR] No se encuentra la base de datos en %ORIGEN%
    pause
    exit /b 1
)

copy "%ORIGEN%" "%DESTINO%"
if %errorlevel% equ 0 (
    echo [OK] Copia creada con exito en: %DESTINO%
) else (
    echo [ERROR] No se pudo copiar el archivo.
)
pause
"@
    [System.IO.File]::WriteAllText((Join-Path $dirBackup "HACER_BACKUP_ACCESS.bat"), $backupBat, $utf8Bom)

    # Root batch helpers
    $iniciarTodoBat = @"
@echo off
title ReprediSL V4 - Centro de Control
cd /d "%~dp0ReprediTrayDaemon"
start "" "ReprediTrayDaemon.exe" --start-all
exit
"@
    [System.IO.File]::WriteAllText((Join-Path $target "INICIAR_TODO.bat"), $iniciarTodoBat, $utf8Bom)

    $detenerTodoBat = @"
@echo off
title ReprediSL V4 - Detener Servicios
taskkill /f /im sincronizador.exe 2>nul
taskkill /f /im postgrest.exe 2>nul
taskkill /f /im caddy.exe 2>nul
taskkill /f /im ReprediTrayDaemon.exe 2>nul
echo Todos los servicios de ReprediSL han sido detenidos.
pause
"@
    [System.IO.File]::WriteAllText((Join-Path $target "DETENER_TODO.bat"), $detenerTodoBat, $utf8Bom)

    # Crear accesos directos
    $crearAccesosBat = @"
@echo off
chcp 65001 > nul
title Crear Accesos Directos ReprediSL
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "`$ws = New-Object -ComObject WScript.Shell; `$s = `$ws.CreateShortcut([System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'ReprediSL Centro de Control.lnk')); `$s.TargetPath = '%~dp0ReprediTrayDaemon\ReprediTrayDaemon.exe'; `$s.WorkingDirectory = '%~dp0ReprediTrayDaemon'; `$s.IconLocation = '%~dp0ReprediTrayDaemon\favicon.ico'; `$s.Save(); Write-Host '[OK] Acceso directo creado en el Escritorio.'"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "`$ws = New-Object -ComObject WScript.Shell; `$s = `$ws.CreateShortcut([System.IO.Path]::Combine([Environment]::GetFolderPath('Startup'), 'ReprediSL Centro de Control.lnk')); `$s.TargetPath = '%~dp0ReprediTrayDaemon\ReprediTrayDaemon.exe'; `$s.Arguments = '--start-all'; `$s.WorkingDirectory = '%~dp0ReprediTrayDaemon'; `$s.IconLocation = '%~dp0ReprediTrayDaemon\favicon.ico'; `$s.Save(); Write-Host '[OK] Acceso directo anadido al inicio de Windows (Startup).'"
pause
"@
    [System.IO.File]::WriteAllText((Join-Path $target "CREAR_ACCESOS_DIRECTOS.bat"), $crearAccesosBat, $utf8Bom)

    # Archivo LEEME
    $leemeTxt = @"
================================================================================
 REPREDISL V4 - GUIA DE INSTALACIÓN Y OPERACIÓN EN CLIENTE
================================================================================

Directorio Base: $target

Estructura de Carpetas:
  ├── ReprediTrayDaemon\
  │     ├── ReprediTrayDaemon.exe   (Centro de Control en bandeja de sistema)
  │     ├── *.dll                   (Librerías de .NET 10 y Npgsql)
  │     ├── config.json             (Configuración principal de rutas y puertos)
  │     ├── postgrest.exe           (API REST de PostgREST)
  │     └── postgrest.conf          (Configuración de PostgREST)
  │
  ├── Sync\
  │     ├── sincronizador.exe       (Sincronizador de pedidos PostgreSQL -> Access)
  │     ├── config.json             (Configuración del sincronizador)
  │     └── SincronizarPedidosEntrantes.ps1 (Lógica canónica de inserción en Access)
  │
  ├── Caddy\
  │     ├── caddy.exe               (Proxy inverso HTTPS / Let's Encrypt)
  │     └── Caddyfile               (Reglas de enrutamiento a api.repredisl.com)
  │
  ├── Logs\                         (Registro histórico de sincronización y errores)
  │     ├── sync_progress.log
  │     └── sync_errors.log
  │
  └── Backup\                       (Copias de seguridad de la base de datos gestion.mdb)
        └── HACER_BACKUP_ACCESS.bat

Acciones Rápidas:
  1. Para crear el icono en el Escritorio e Inicio de Windows:
     Ejecutar 'CREAR_ACCESOS_DIRECTOS.bat' como Administrador.

  2. Para iniciar todo el sistema:
     Doble clic en 'INICIAR_TODO.bat' o abrir 'ReprediTrayDaemon.exe'.
     El icono aparecerá en la barra de tareas junto al reloj.

  3. Base de Datos Access por defecto:
     C:\pensi\psgestw\e0012026\gestion.mdb (modificable en config.json).
================================================================================
"@
    [System.IO.File]::WriteAllText((Join-Path $target "LEEME_INSTALACION.txt"), $leemeTxt, $utf8Bom)
}

# 5. Limpieza de temporales
Write-Host "[5/6] Limpiando carpetas temporales de compilación..." -ForegroundColor Yellow
if (Test-Path $tempPublishDaemon) { Remove-Item $tempPublishDaemon -Recurse -Force }
if (Test-Path $tempPublishSync) { Remove-Item $tempPublishSync -Recurse -Force }

# 6. Resumen
Write-Host "[6/6] ¡Instalación para cliente preparada con éxito!" -ForegroundColor Green
Write-Host ""
Write-Host "Ubicaciones generadas:" -ForegroundColor Cyan
foreach ($target in $rutasDestino) {
    Write-Host "  -> $target" -ForegroundColor Green
}
Write-Host ""