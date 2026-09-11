param (
    [string]$DbMdb = ""
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

# Cargar configuracion centralizada PsForce
$loaderCandidates = @(
    (Join-Path $ScriptDir "..\Despliegue\Load-PsForceConfig.ps1"),
    (Join-Path $ScriptDir "..\Load-PsForceConfig.ps1"),
    (Join-Path $ScriptDir "Load-PsForceConfig.ps1"),
    "C:\Pensi\PsForce\Scripts\Despliegue\Load-PsForceConfig.ps1"
)
$cfg = $null
foreach ($cand in $loaderCandidates) {
    if (Test-Path -LiteralPath $cand) {
        $cfg = & $cand
        break
    }
}

if (-not $DbMdb -and $cfg) {
    $DbMdb = $cfg.AccessDbPath
}

$pathMdb = Get-AbsolutePath $DbMdb
$logPath = if ($cfg) { Join-Path $cfg.LogsPath "sync_progress.log" } else { Join-Path $ProjectRoot "Logs\sync_progress.log" }

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " EJECUTOR DE EXPORTACION ACCESS -> POSTGRESQL (EXTERNO)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

if (-not (Test-Path $pathMdb)) {
    $errMsg = "La base de datos MDB no existe: $pathMdb"
    Write-Error $errMsg
    Add-Content -Path $logPath -Value "[$((Get-Date).ToString('HH:mm:ss'))] [ERROR] $errMsg" -Encoding UTF8
    Exit 1
}

# Limpieza previa de cualquier proceso colgado de Access
Get-Process -Name MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$ahoraStr = (Get-Date).ToString('HH:mm:ss')
Add-Content -Path $logPath -Value "[$ahoraStr] [EXPORTACION] Iniciando exportacion de $pathMdb a PostgreSQL..." -Encoding UTF8

Write-Host "[1/3] Abriendo base de datos MDB en segundo plano..." -ForegroundColor Yellow
Write-Host "      Ruta ERP (gestion.mdb): $pathMdb" -ForegroundColor Gray

$accessApp = New-Object -ComObject Access.Application
$accessApp.Visible = $false
$accessApp.UserControl = $false

try {
    $accessApp.OpenCurrentDatabase($pathMdb)
    Write-Host "      Base MDB abierta correctamente." -ForegroundColor Green

    Write-Host "[2/3] Importando y ejecutando sincronizacion ('ExportarTablas')..." -ForegroundColor Yellow
    Write-Host "      Tablas afectadas: clientes, vendedores, tarifas, precios, uventas" -ForegroundColor Gray
    
    $pathModulo = Get-AbsolutePath "src/Access/modActBdApi.bas"
    if (Test-Path $pathModulo) {
        # 1. Eliminar modulo previo si existe para forzar la actualizacion limpia
        try {
            $accessApp.DoCmd.DeleteObject(5, "modActBdApi") # 5 = acModule
        } catch {}

        # 2. Cargar/Importar modulo modActBdApi.bas en gestion.mdb y GUARDARLO explícitamente
        $loadedOk = $false
        try {
            $accessApp.LoadFromText(5, "modActBdApi", $pathModulo)
            $accessApp.DoCmd.Save(5, "modActBdApi")
            $loadedOk = $true
            Write-Host "      Modulo modActBdApi importado mediante LoadFromText OK." -ForegroundColor Gray
        } catch {
            Write-Host "      [INFO] LoadFromText fallo: $_" -ForegroundColor Yellow
            try {
                if ($accessApp.VBE -and $accessApp.VBE.ActiveVBProject) {
                    $accessApp.VBE.ActiveVBProject.VBComponents.Import($pathModulo) | Out-Null
                    $accessApp.DoCmd.Save(5, "modActBdApi")
                    $loadedOk = $true
                    Write-Host "      Modulo modActBdApi importado mediante VBE OK." -ForegroundColor Gray
                }
            } catch {
                Write-Host "      [AVISO] Intento de importacion VBE fallo: $_" -ForegroundColor Yellow
            }
        }
    }

    # Ejecutar la funcion principal de exportacion (Eval / Run)
    try {
        [void]$accessApp.Eval("ExportarTablas()")
    } catch {
        try {
            $accessApp.Run("ExportarTablas")
        } catch {
            $accessApp.Run("modActBdApi.ExportarTablas")
        }
    }
    
    $sw.Stop()
    $totalSeg = [math]::Round($sw.Elapsed.TotalSeconds, 2)
    Write-Host "[3/3] Exportacion a PostgreSQL y recarga de PostgREST OK!" -ForegroundColor Green
    Write-Host "      Tiempo total de sincronizacion: $totalSeg segundos" -ForegroundColor Cyan
    Write-Host "=========================================================" -ForegroundColor Cyan

    Add-Content -Path $logPath -Value "[$((Get-Date).ToString('HH:mm:ss'))] [OK] Exportacion de $pathMdb a PostgreSQL finalizada con exito ($totalSeg seg)." -Encoding UTF8

} catch {
    $errText = $_.Exception.Message
    if (-not $errText) { $errText = $_.ToString() }
    Write-Host "      [ERROR] Fallo durante la exportacion: $errText" -ForegroundColor Red
    Add-Content -Path $logPath -Value "[$((Get-Date).ToString('HH:mm:ss'))] [ERROR] Fallo durante la exportacion: $errText" -Encoding UTF8
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

