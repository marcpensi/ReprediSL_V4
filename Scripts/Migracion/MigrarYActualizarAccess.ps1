param (
    [string]$DbOrigen  = "C:\pensi\psgestw\e0012026\gestion.mdb",
    [string]$DbDestino = "C:\pensi\psgestw\e0012026\gestion.mdb",
    [string]$DbReferencia = "src/Access/BdNewRepre.mdb",
    [string]$ModuloBas = "src/Access/modActBdApi.bas"
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$ProjectRoot = (Get-Item (Join-Path $ScriptDir "..\..")).FullName

function Get-AbsolutePath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) {
        return $path
    }
    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $path))
}

$pathOrigen = Get-AbsolutePath $DbOrigen
$pathDestino = Get-AbsolutePath $DbDestino
$pathModulo = Get-AbsolutePath $ModuloBas

if (-not $pathDestino.ToLower().EndsWith(".mdb")) {
    $pathDestino = [System.IO.Path]::ChangeExtension($pathDestino, ".mdb")
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " ACTUALIZADOR MDB AUTOMATICO ACCESS -> REPREDISL V4" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

if (-not (Test-Path $pathOrigen)) {
    Write-Error "La base de datos origen MDB no existe: $pathOrigen"
}

# Limpieza previa de procesos Access colgados
Get-Process -Name MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# 1. Copiar origen a destino (solo si son archivos distintos)
$ldbPath = [System.IO.Path]::ChangeExtension($pathDestino, ".ldb")
if (Test-Path $ldbPath) {
    try { Remove-Item -Path $ldbPath -Force -ErrorAction SilentlyContinue } catch {}
}

if ($pathOrigen -ne $pathDestino) {
    Write-Host "[1/4] Copiando base origen a destino..." -ForegroundColor Yellow
    Copy-Item -Path $pathOrigen -Destination $pathDestino -Force
    Write-Host "      Base MDB copiada en: $pathDestino" -ForegroundColor Green
} else {
    Write-Host "[1/4] Origen y destino son el mismo archivo. Actualizando en su lugar: $pathDestino" -ForegroundColor Yellow
}

# 2. Iniciar Access COM Automation
Write-Host "[2/4] Abriendo Access COM Automation (Formato .mdb)..." -ForegroundColor Yellow
$accessApp = New-Object -ComObject Access.Application
$accessApp.Visible = $false
$accessApp.UserControl = $false

$db = $null

try {
    $accessApp.OpenCurrentDatabase($pathDestino)
    $db = $accessApp.CurrentDb()

    Write-Host "[3/3] Inyectando modulo VBA ($ModuloBas)..." -ForegroundColor Yellow
    if (Test-Path $pathModulo) {
        try {
            $accessApp.LoadFromText(5, "modActBdApi", $pathModulo) # acModule = 5
            Write-Host "      Modulo modActBdApi.bas inyectado correctamente en el MDB." -ForegroundColor Green
        } catch {
            Write-Host "      Aviso al inyectar modulo VBA: $_" -ForegroundColor Yellow
        }
    }

    Write-Host "=========================================================" -ForegroundColor Cyan
    Write-Host " PROCESO COMPLETADO EXITOSAMENTE (FORMATO .MDB)" -ForegroundColor Green
    Write-Host " Base de datos final lista en: $pathDestino" -ForegroundColor Green
    Write-Host "=========================================================" -ForegroundColor Cyan

} finally {
    # Cierre estricto de conexiones y eliminación forzada del proceso Access y candado .ldb
    if ($db) {
        try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($db) | Out-Null } catch {}
        $db = $null
    }
    if ($accessApp) {
        try { $accessApp.CloseCurrentDatabase() } catch {}
        try { $accessApp.Quit() } catch {}
        try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($accessApp) | Out-Null } catch {}
        $accessApp = $null
    }
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()

    # Detener el proceso de Access e incondicionalmente remover el archivo .ldb resultante
    Get-Process -Name MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    if (Test-Path $ldbPath) {
        try { Remove-Item -Path $ldbPath -Force -ErrorAction SilentlyContinue } catch {}
    }
}
