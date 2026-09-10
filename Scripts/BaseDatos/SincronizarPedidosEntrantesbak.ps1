param (
    [switch]$Loop = $false,
    [int]$IntervaloSegundos = 3
)

$ErrorActionPreference = "Continue"

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$ProjectRoot = (Get-Item (Join-Path $ScriptDir "..\..")).FullName

$logPath = Join-Path $ProjectRoot "src/Access/sync_progress.log"
$pathMdb = "C:\pensi\psgestw\e0012026\gestion.mdb"
if (-not (Test-Path $pathMdb)) {
    $pathMdb = Join-Path $ProjectRoot "src/Access/E0012026/gestion.mdb"
}
if (-not (Test-Path $pathMdb)) {
    $pathMdb = Join-Path $ProjectRoot "src/Access/gestion.mdb"
}

function Log-Sync([string]$msg) {
    $timestamp = (Get-Date).ToString("HH:mm:ss")
    $line = "[$timestamp] $msg"
    Write-Host $line
    try {
        [System.IO.File]::AppendAllText($logPath, "$line`r`n", [System.Text.Encoding]::UTF8)
    } catch {
        # Silencioso si otro proceso está leyendo el log
    }
}

function Sincronizar-Pedidos-UnaVez {
    # 1. Consultar pedidos pendientes en PostgreSQL
    $sqlSelect = "SELECT id, id_empresa, ejercicio, serie, numero_pedido, fecha, id_cliente, cliente, id_forma_pago, id_tarifa, total, canal, id_vendedor, lineas::text as lineas_json FROM public.pedidos_nuevos WHERE estado = 'N' AND synced_at IS NULL ORDER BY id ASC;"
    
    $cmd = "psql -U postgres -d repredisl_api -t -A -F `"`t`" -c `"$sqlSelect`""
    $rows = Invoke-Expression $cmd

    if (-not $rows) { return 0 }

    $count = 0
    $cn = $null

    try {
        $cn = New-Object -ComObject ADODB.Connection
        $cn.Open("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$pathMdb")

        foreach ($row in $rows) {
            if ([string]::IsNullOrWhiteSpace($row)) { continue }
            $fields = $row.Split("`t")
            if ($fields.Length -lt 14) { continue }

            $idPg        = $fields[0].Trim()
            $idEmpresa   = if ($fields[1].Trim()) { [int]$fields[1].Trim() } else { 1 }
            $ejercicio   = if ($fields[2].Trim()) { [int]$fields[2].Trim() } else { [int](Get-Date).Year }
            $serie       = if ($fields[3].Trim()) { $fields[3].Trim() } else { "VD" }
            $numPedido   = if ($fields[4].Trim()) { [int]$fields[4].Trim() } else { 2988 }
            $fechaStr    = $fields[5].Trim()
            $idCliente   = if ($fields[6].Trim()) { [int]$fields[6].Trim() } else { 1001 }
            $clienteNom  = $fields[7].Trim()
            $idFormaPago = if ($fields[8].Trim()) { [int]$fields[8].Trim() } else { 1 }
            $idTarifa    = if ($fields[9].Trim()) { [int]$fields[9].Trim() } else { 1 }
            $totalStr    = $fields[10].Trim().Replace(",", ".")
            $canal       = if ($fields[11].Trim()) { $fields[11].Trim() } else { "movil" }
            $idVendedor  = if ($fields[12].Trim()) { [int]$fields[12].Trim() } else { 13 }
            $lineasJson  = $fields[13].Trim()

            $numPedDisplay = if ($serie) { "$serie-$numPedido" } else { "VD-$numPedido" }
            Log-Sync "[NUEVO PEDIDO] Recibido pedido N. $numPedDisplay | Serie: $serie | Cliente: $idCliente ($clienteNom) | Importe: $totalStr EUR"

            # 2. Insertar en PedidosCab de Access
            $sqlCab = "INSERT INTO PedidosCab (IdEmpresa, Ejercicio, Serie, NumPedido, Fecha, IdCliente, IdFormaPago, IdTarifa, Total, Canal, IdVendedor) VALUES ($idEmpresa, $ejercicio, '$serie', $numPedido, Now(), $idCliente, $idFormaPago, $idTarifa, $totalStr, '$canal', $idVendedor);"
            try {
                $cn.Execute($sqlCab) | Out-Null
            } catch {
                Log-Sync "[WARN] PedidosCab insercion aviso/error: $_"
            }

            # 3. Insertar líneas en PedidosLin de Access
            if ($lineasJson) {
                try {
                    $items = ConvertFrom-Json $lineasJson
                    $numLinea = 1
                    foreach ($item in $items) {
                        $idArticulo = if ($item.code) { $item.code } else { 1 }
                        $desc = if ($item.desc) { $item.desc.Replace("'", "''") } else { "Articulo" }
                        $qty = if ($item.qty) { [double]$item.qty } else { 1 }
                        $price = if ($item.price) { [double]$item.price } else { 0 }
                        $neto = [Math]::Round($qty * $price, 2)

                        $priceStr = $price.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                        $netoStr  = $neto.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                        $qtyStr   = $qty.ToString([System.Globalization.CultureInfo]::InvariantCulture)

                        $sqlLin = "INSERT INTO PedidosLin (IdEmpresa, Ejercicio, Serie, NumPedido, NumLinea, IdArticulo, Descripcion, Cajas, UdsCaja, Unidades, Precio, Dto1, Dto2, ImporteNeto, IdTipoIva, Iva, Recargo) VALUES ($idEmpresa, $ejercicio, '$serie', $numPedido, $numLinea, '$idArticulo', '$desc', 1, $qtyStr, $qtyStr, $priceStr, 0, 0, $netoStr, 1, 21, 0);"
                        $cn.Execute($sqlLin) | Out-Null
                        $numLinea++
                    }
                } catch {
                    Log-Sync "[WARN] Error parseando/insertando lineas: $_"
                }
            }

            # 4. Marcar como sincronizado en PostgreSQL
            $sqlUpdate = "UPDATE public.pedidos_nuevos SET estado = 'S', synced_at = NOW() WHERE id = $idPg;"
            Invoke-Expression "psql -U postgres -d repredisl_api -c `"$sqlUpdate`"" | Out-Null

            Log-Sync "[SYNC] INSERT INTO PedidosCab (Serie, NumPedido, Cliente, Total) VALUES ('$serie', $numPedido, '$clienteNom', $totalStr) -> Confirmado en gestion.mdb ($pathMdb) [OK]."
            $count++
        }
    } catch {
        Log-Sync "[ERROR] Error conectando a Access o procesando sincronizacion: $_"
    } finally {
        if ($cn -and $cn.State -ne 0) {
            $cn.Close()
        }
    }

    return $count
}

if ($Loop) {
    Write-Host "Iniciando servicio de sincronizacion de pedidos entrantes (cada $IntervaloSegundos s)..." -ForegroundColor Cyan
    while ($true) {
        Sincronizar-Pedidos-UnaVez
        Start-Sleep -Seconds $IntervaloSegundos
    }
} else {
    $n = Sincronizar-Pedidos-UnaVez
    Write-Host "Sincronizacion ejecutada. $n pedido(s) procesado(s)." -ForegroundColor Green
}
