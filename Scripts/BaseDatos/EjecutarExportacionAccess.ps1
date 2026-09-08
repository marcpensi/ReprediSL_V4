param (
    [string]$DbMdb = "src/Access/BdDestino.mdb"
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

$pathMdb = Get-AbsolutePath $DbMdb

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " EJECUTOR DE EXPORTACION ACCESS -> POSTGRESQL (EXTERNO)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

if (-not (Test-Path $pathMdb)) {
    Write-Error "La base de datos MDB no existe: $pathMdb"
}

# Limpieza previa de cualquier proceso colgado de Access
Get-Process -Name MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

$sw = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "[1/3] Abriendo base de datos MDB en segundo plano..." -ForegroundColor Yellow
Write-Host "      Ruta: $pathMdb" -ForegroundColor Gray

$accessApp = New-Object -ComObject Access.Application
$accessApp.Visible = $false
$accessApp.UserControl = $false

try {
    $accessApp.OpenCurrentDatabase($pathMdb)
    Write-Host "      Base MDB abierta correctamente." -ForegroundColor Green

    Write-Host "[2/3] Ejecutando sincronizacion masiva en lotes ('ExportarTablas')..." -ForegroundColor Yellow
    Write-Host "      Tablas afectadas: clientes, vendedores, tarifas, precios, uventas" -ForegroundColor Gray
    
    $pathModulo = Get-AbsolutePath "src/Access/modActBdApi.bas"
    if (Test-Path $pathModulo) {
        try {
            $accessApp.LoadFromText(5, "modActBdApi", $pathModulo)
        } catch {}
    }

    $accessApp.Run("ExportarTablas")
    
    $sw.Stop()
    Write-Host "[3/3] Exportacion a PostgreSQL y recarga de PostgREST OK!" -ForegroundColor Green
    Write-Host "      Tiempo total de sincronización: $([math]::Round($sw.Elapsed.TotalSeconds, 2)) segundos" -ForegroundColor Cyan
    Write-Host "=========================================================" -ForegroundColor Cyan

} catch {
    Write-Host "      [ERROR] Fallo durante la exportacion: $_" -ForegroundColor Red
} finally {
    if ($accessApp) {
        try { $accessApp.CloseCurrentDatabase() } catch {}
        try { $accessApp.Quit() } catch {}
        try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($accessApp) | Out-Null } catch {}
        $accessApp = $null
    }
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Get-Process -Name MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    
    $ldbPath = [System.IO.Path]::ChangeExtension($pathMdb, ".ldb")
    if (Test-Path $ldbPath) {
        try { Remove-Item -Path $ldbPath -Force -ErrorAction SilentlyContinue } catch {}
    }
}
