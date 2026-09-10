param (
    [string]$DbOrigen = "C:\pensi\psgestw\e0012026\gestion.mdb",
    [string]$DbDestino = "src/Access/gestion.mdb",
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

# 1. Copiar origen a destino
Write-Host "[1/4] Copiando base origen (.MDB) a destino (.mdb)..." -ForegroundColor Yellow

$ldbPath = [System.IO.Path]::ChangeExtension($pathDestino, ".ldb")
if (Test-Path $ldbPath) {
    try { Remove-Item -Path $ldbPath -Force -ErrorAction SilentlyContinue } catch {}
}

Copy-Item -Path $pathOrigen -Destination $pathDestino -Force
Write-Host "      Base MDB copiada con exito en: $pathDestino" -ForegroundColor Green

# 2. Iniciar Access COM Automation
Write-Host "[2/4] Abriendo Access COM Automation (Formato .mdb)..." -ForegroundColor Yellow
$accessApp = New-Object -ComObject Access.Application
$accessApp.Visible = $false
$accessApp.UserControl = $false

$db = $null

try {
    $accessApp.OpenCurrentDatabase($pathDestino)
    $db = $accessApp.CurrentDb()

    Write-Host "[3/4] Creando / actualizando consultas API en MDB..." -ForegroundColor Yellow
    
    $queries = [ordered]@{
        "QryClientesApi" = "SELECT CodCliente AS id_cliente, CodCliente AS codigo, NombreFiscal AS nombre, NombreComercial AS nombre_comercial, NifCIF AS nif, Telefono AS telefono, Movil AS movil, Email AS email, Web AS web, Direccion AS street, CodPostal AS codigo_postal, Poblacion AS city, Provincia AS state, DireccionEnvio AS direccionenvio, CodPostalEnvio AS cpostalenvio, PoblacionEnvio AS poblacionenvio, ProvinciaEnvio AS provinciaenvio, Banco AS nombre_banco, IBAN AS cuenta_bancaria, CodVendedor AS id_vendedor FROM Clientes";
        "QryVendedoresApi" = "SELECT CodVendedor AS id_vendedor, Nombre AS nombre_vendedor, Serie AS serie FROM Vendedores";
        "QryTarifasApi" = "SELECT CodTarifa AS id_tarifa, NombreTarifa AS nombre_tarifa, Descuento AS descuento FROM Tarifas";
        "QryPreciosApi" = "SELECT CodProducto AS id_producto, CodProducto AS codigo, CodTarifa AS id_tarifa, PrecioVenta AS precio_venta FROM Precios";
        "QryUventasApi" = "SELECT CodProducto AS id_producto, CodProducto AS codigo, UnidadesCaja AS unidades_caja, UnidadVenta AS unidad_venta FROM Uventas"
    }

    foreach ($qName in $queries.Keys) {
        $sqlText = $queries[$qName]
        $qdf = $null
        try {
            $qdf = $db.QueryDefs($qName)
            $qdf.SQL = $sqlText
            Write-Host "      Consulta ${qName} actualizada." -ForegroundColor Gray
        } catch {
            try {
                $qdf = $db.CreateQueryDef($qName, $sqlText)
                Write-Host "      Consulta ${qName} creada." -ForegroundColor Green
            } catch {
                Write-Host "      Aviso creando ${qName}: $_" -ForegroundColor Yellow
            }
        } finally {
            if ($qdf) {
                try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($qdf) | Out-Null } catch {}
                $qdf = $null
            }
        }
    }

    Write-Host "[4/4] Inyectando modulo VBA ($ModuloBas)..." -ForegroundColor Yellow
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
