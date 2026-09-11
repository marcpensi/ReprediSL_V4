param (
    [switch]$Loop = $false,
    [int]$IntervaloSegundos = 3
)

$ErrorActionPreference = "Stop"

# PostgreSQL PsForce
$env:PGUSER = "postgres"
$env:PGHOST = "localhost"
$env:PGPORT = "5433"
$env:PGDBAPI = "repredi-api"
$env:PGCLIENTENCODING = "UTF8"

$env:PGPASSWORD = $env:PGREPREAPIPWD # psql/libpq solo reconoce PGPASSWORD

# Consola UTF-8
[Console]::InputEncoding  = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$ProjectRoot = "C:\Pensi\PsForce"

# Carpeta centralizada de logs PsForce
$logPath = Join-Path $ProjectRoot "Logs"
if (-not (Test-Path -LiteralPath $logPath)) {
    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
}

$logSyncFile       = Join-Path $logPath "sync_progress.log"
$logErrorsFile     = Join-Path $logPath "errors.log"
$logPendientesFile = Join-Path $logPath "pedidos_pendientes.log"
$logDescartadosFile = Join-Path $logPath "pedidos_descartados.log"

# PostgreSQL PsForce
$Psql = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
if (-not (Test-Path -LiteralPath $Psql)) {
    throw "No se encuentra PostgreSQL 17: $Psql"
}

$pathMdb = "C:\PENSI\PSGESTW\E0012026\gestion.mdb"
if (-not (Test-Path -LiteralPath $pathMdb)) {
    $pathMdb = Join-Path $ProjectRoot "Test\E0012026\gestion.mdb"
}
if (-not (Test-Path -LiteralPath $pathMdb)) {
    $pathMdb = Join-Path $ProjectRoot "Test\gestion.mdb"
}

function Write-LogFile([string]$FilePath, [string]$Message) {
    try {
        $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        $line = "[$timestamp] $Message"
        $utf8Bom = New-Object System.Text.UTF8Encoding($true)
        [System.IO.File]::AppendAllText($FilePath, "$line`r`n", $utf8Bom)
    }
    catch {
        Write-Warning "No se pudo escribir en el registro '$FilePath': $($_.Exception.Message)"
    }
}

function Log-Sync([string]$Message) {
    $timestamp = (Get-Date).ToString("HH:mm:ss")
    $line = "[$timestamp] $Message"
    Write-Host $line
    Write-LogFile $logSyncFile $Message
}

function Log-Error([string]$Message) {
    Write-LogFile $logErrorsFile $Message
}

function Log-Pendiente([string]$Message) {
    Write-LogFile $logPendientesFile $Message
}

function Log-Descartado([string]$Message) {
    Write-LogFile $logDescartadosFile $Message
}

function Convertir-Decimal([object]$Value, [double]$DefaultValue = 0.0) {
    if ($Value -is [System.Array]) {
        $Value = @($Value)[0]
    }

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace("$Value")) {
        return $DefaultValue
    }

    $text = "$Value".Trim().Replace("€", "").Replace("pta", "").Trim()

    if ($text.Contains(",") -and $text.Contains(".")) {
        $text = $text.Replace(".", "").Replace(",", ".")
    }
    elseif ($text.Contains(",")) {
        $text = $text.Replace(",", ".")
    }

    return [Convert]::ToDouble(
        $text,
        [System.Globalization.CultureInfo]::InvariantCulture
    )
}

function Sql-Decimal([object]$Value) {
    $number = Convertir-Decimal $Value
    return $number.ToString(
        "0.00####",
        [System.Globalization.CultureInfo]::InvariantCulture
    )
}

function Sql-Text([object]$Value) {
    if ($Value -is [System.Array]) {
        $Value = @($Value)[0]
    }
    if ($null -eq $Value) { return "" }
    return ([string]$Value).Replace("'", "''")
}

function Resolver-TipoIva([double]$Porcentaje) {
    $tipos = @(
        @{ Codigo = 1; Porcentaje = 10.0 },
        @{ Codigo = 2; Porcentaje = 21.0 },
        @{ Codigo = 3; Porcentaje = 4.0 },
        @{ Codigo = 4; Porcentaje = 0.0 },
        @{ Codigo = 5; Porcentaje = 0.0 },
        @{ Codigo = 6; Porcentaje = 5.0 },
        @{ Codigo = 7; Porcentaje = 2.0 },
        @{ Codigo = 8; Porcentaje = 7.5 }
    )

    $coincidencia = $tipos |
        Sort-Object { [Math]::Abs($_.Porcentaje - $Porcentaje) } |
        Select-Object -First 1

    if ([Math]::Abs($coincidencia.Porcentaje - $Porcentaje) -gt 0.15) {
        throw "No se puede asociar el IVA calculado ($Porcentaje %) con TiposIva."
    }

    return [int]$coincidencia.Codigo
}

function Obtener-Items([string]$Json) {
    if ([string]::IsNullOrWhiteSpace($Json)) { return @() }

    $parsedItems = ConvertFrom-Json -InputObject $Json
    if ($parsedItems -is [System.Array]) {
        return $parsedItems
    }
    return @($parsedItems)
}

function Sincronizar-Pedidos-UnaVez {
    if (-not (Test-Path -LiteralPath $pathMdb)) {
        throw "No se encuentra gestion.mdb en ninguna ruta configurada."
    }

    $sqlSelect = @"
SELECT id, id_empresa, ejercicio, serie, numero_pedido, fecha, id_cliente,
       cliente, id_forma_pago, id_tarifa, total, canal, id_vendedor,
       lineas::text AS lineas_json
FROM public.pedidos_nuevos
WHERE estado = 'N' AND synced_at IS NULL
ORDER BY id ASC;
"@

    $rows = & $Psql -X -v ON_ERROR_STOP=1 -h $env:PGHOST -p $env:PGPORT -U postgres -d repredisl_api `
        -t -A -F "`t" -c $sqlSelect

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudieron consultar los pedidos pendientes en PostgreSQL."
    }
    if (-not $rows) { return 0 }

    $count = 0
    $cn = New-Object -ComObject ADODB.Connection

    try {
        $cn.Open("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$pathMdb")

        foreach ($rowValue in @($rows)) {
            $row = [string]$rowValue
            if ([string]::IsNullOrWhiteSpace($row)) { continue }

            $fields = $row.Split("`t")
            if ($fields.Length -lt 14) {
                Log-Sync "[ERROR] PostgreSQL devolvió una fila incompleta; no se procesa."
                Log-Error "PostgreSQL devolvió una fila incompleta; no se procesa."
                Log-Descartado "Fila PostgreSQL incompleta. Contenido: $row"
                continue
            }

            $idPg = [long]$fields[0].Trim()
            $serie = Sql-Text $(if ($fields[3].Trim()) { $fields[3].Trim() } else { "VD" })
            $numPedido = [int]$fields[4].Trim()
            $idCliente = [long]$fields[6].Trim()
            $clienteNom = Sql-Text $fields[7].Trim()
            $formaPago = Sql-Text $(if ($fields[8].Trim()) { $fields[8].Trim() } else { "1" })
            $idTarifa = if ($fields[9].Trim()) { [int]$fields[9].Trim() } else { 1 }
            $total = Convertir-Decimal $fields[10]
            $idVendedor = if ($fields[12].Trim()) { [int]$fields[12].Trim() } else { 13 }
            $items = @(Obtener-Items $fields[13].Trim())

            if ($items.Count -eq 0) {
                Log-Sync "[ERROR] Pedido $serie-$numPedido sin líneas; permanece pendiente."
                Log-Pendiente "Pedido $serie-$numPedido sin líneas; permanece pendiente."
                continue
            }

            $lineas = @()
            $importeBase = 0.0
            $numeroLinea = 1

            foreach ($itemValue in $items) {
                $item = $itemValue
                if ($item -is [System.Array]) { $item = @($item)[0] }

                $unidades = Convertir-Decimal $item.qty 1.0
                $precio = Convertir-Decimal $item.price 0.0
                $importe = [Math]::Round(
                    $unidades * $precio,
                    2,
                    [MidpointRounding]::AwayFromZero
                )

                $tipoIvaItem = $null
                if ($null -ne $item.tipoIva -and "$($item.tipoIva)" -ne "") {
                    $tipoIvaItem = [int](@($item.tipoIva)[0])
                }

                $lineas += [pscustomobject]@{
                    Numero      = $numeroLinea
                    Articulo    = Sql-Text $item.code
                    Descripcion = Sql-Text $(if ($item.desc) { $item.desc } else { "Artículo" })
                    Unidades    = $unidades
                    Precio      = $precio
                    Importe     = $importe
                    TipoIva     = $tipoIvaItem
                }

                $importeBase += $importe
                $numeroLinea++
            }

            $importeBase = [Math]::Round(
                $importeBase,
                2,
                [MidpointRounding]::AwayFromZero
            )
            $importeIva = [Math]::Round(
                $total - $importeBase,
                2,
                [MidpointRounding]::AwayFromZero
            )

            if ($importeBase -lt 0) {
                throw "La base del pedido $serie-$numPedido no puede ser negativa."
            }
            if ($importeIva -lt 0) {
                throw "El total del pedido $serie-$numPedido es inferior a su base imponible."
            }

            $porcentajeGlobal = if ($importeBase -eq 0) {
                0.0
            } else {
                [Math]::Round(($importeIva / $importeBase) * 100, 2)
            }
            $tipoIvaGlobal = Resolver-TipoIva $porcentajeGlobal

            foreach ($linea in $lineas) {
                if ($null -eq $linea.TipoIva) {
                    $linea.TipoIva = $tipoIvaGlobal
                }
            }

            $baseSql = Sql-Decimal $importeBase
            $ivaSql = Sql-Decimal $importeIva
            $totalSql = Sql-Decimal $total

            Log-Sync "[NUEVO PEDIDO] $serie-$numPedido | Cliente: $idCliente ($clienteNom) | Total: $totalSql EUR"

            $cn.BeginTrans() | Out-Null
            try {
                $rs = $cn.Execute(
                    "SELECT COUNT(*) AS N FROM PedVentas " +
                    "WHERE Serie='$serie' AND Numero=$numPedido;"
                )
                $yaExiste = [int]$rs.Fields.Item("N").Value -gt 0
                $rs.Close()

                if ($yaExiste) {
                    $cn.RollbackTrans() | Out-Null
                    Log-Sync "[INFO] El pedido $serie-$numPedido ya existe en PedVentas (Access). Marcando como sincronizado en PostgreSQL para evitar duplicados."
                    Log-Descartado "Pedido $serie-$numPedido ya existía en Access; marcado como sincronizado en PostgreSQL para evitar duplicado."
                    $sqlUpdateYaExiste = @"
UPDATE public.pedidos_nuevos
SET estado = 'S', synced_at = NOW()
WHERE id = $idPg AND estado = 'N';
"@
                    & $Psql -X -v ON_ERROR_STOP=1 -h $env:PGHOST -p $env:PGPORT -U postgres -d repredisl_api `
                        -c $sqlUpdateYaExiste | Out-Null
                    $count++
                    continue
                }

                $sqlCabecera = @"
INSERT INTO PedVentas
(Serie, Numero, Cliente, Fecha, Descuento, DescuentoPP, Irpf, FormaPago,
 RecargoSN, Tarifa, Kilometros, Vendedor, TipoVenta, EnlazadoSN, ImpresoSN,
 ImporteBruto, ImporteDto, ImporteDtoPP, ImporteBase, ImporteIva,
 ImporteRec, ImporteTotal, ImporteManoObra)
VALUES
('$serie', $numPedido, $idCliente, Now(), 0, 0, 0, '$formaPago',
 False, $idTarifa, 0, $idVendedor, 'ODOO', False, False,
 $baseSql, 0, 0, $baseSql, $ivaSql, 0, $totalSql, 0);
"@
                $cn.Execute($sqlCabecera) | Out-Null

                foreach ($linea in $lineas) {
                    $unidadesSql = Sql-Decimal $linea.Unidades
                    $precioSql = Sql-Decimal $linea.Precio
                    $importeLineaSql = Sql-Decimal $linea.Importe

                    $sqlLinea = @"
INSERT INTO LineasPedVentas
(Serie, NumPed, Linea, Tipo, Articulo, Descripcion, Columna1, Columna2,
 Columna3, Unidades, Precio, Descuento, Importe, TipoIva, ReservaSN,
 ImporteCom, UniServidas, UniPedidasCompra)
VALUES
('$serie', $numPedido, $($linea.Numero), 'A', '$($linea.Articulo)',
 '$($linea.Descripcion)', 0, 0, 0, $unidadesSql, $precioSql, 0,
 $importeLineaSql, $($linea.TipoIva), False, 0, 0, 0);
"@
                    $cn.Execute($sqlLinea) | Out-Null
                }

                $gruposIva = $lineas | Group-Object TipoIva
                foreach ($grupo in $gruposIva) {
                    $tipoIva = [int]$grupo.Name
                    $baseGrupo = [Math]::Round(
                        ($grupo.Group | Measure-Object Importe -Sum).Sum,
                        2,
                        [MidpointRounding]::AwayFromZero
                    )

                    $porcentajeGrupo = switch ($tipoIva) {
                        1 { 10.0 }
                        2 { 21.0 }
                        3 { 4.0 }
                        4 { 0.0 }
                        5 { 0.0 }
                        6 { 5.0 }
                        7 { 2.0 }
                        8 { 7.5 }
                        default { throw "TipoIva desconocido: $tipoIva" }
                    }

                    $ivaGrupo = [Math]::Round(
                        $baseGrupo * $porcentajeGrupo / 100,
                        2,
                        [MidpointRounding]::AwayFromZero
                    )
                    $totalGrupo = $baseGrupo + $ivaGrupo

                    $baseGrupoSql = Sql-Decimal $baseGrupo
                    $ivaGrupoSql = Sql-Decimal $ivaGrupo
                    $totalGrupoSql = Sql-Decimal $totalGrupo

                    $sqlIva = @"
INSERT INTO IvaLineasPedVentas
(Serie, NumPed, Tipo, ImporteBruto, ImporteDto, ImporteDtoPP,
 ImporteBase, ImporteIva, ImporteRec, ImporteTotal)
VALUES
('$serie', $numPedido, $tipoIva, $baseGrupoSql, 0, 0,
 $baseGrupoSql, $ivaGrupoSql, 0, $totalGrupoSql);
"@
                    $cn.Execute($sqlIva) | Out-Null
                }

                $cn.CommitTrans() | Out-Null
            }
            catch {
                try { $cn.RollbackTrans() | Out-Null } catch {}
                Log-Sync "[ERROR] Pedido $serie-$numPedido no insertado en Access: $($_.Exception.Message)"
                Log-Error "Pedido $serie-$numPedido no insertado en Access: $($_.Exception.Message)"
                Log-Pendiente "Pedido $serie-$numPedido sigue pendiente por error de inserción en Access."
                continue
            }

            $sqlUpdate = @"
UPDATE public.pedidos_nuevos
SET estado = 'S', synced_at = NOW()
WHERE id = $idPg AND estado = 'N' AND synced_at IS NULL;
"@

            & $Psql -X -v ON_ERROR_STOP=1 -h $env:PGHOST -p $env:PGPORT -U postgres -d repredisl_api `
                -c $sqlUpdate | Out-Null

            if ($LASTEXITCODE -ne 0) {
                Log-Sync "[ERROR] $serie-$numPedido está en Access, pero no se pudo actualizar PostgreSQL."
                Log-Error "$serie-$numPedido está en Access, pero no se pudo actualizar PostgreSQL."
                Log-Pendiente "$serie-$numPedido requiere revisión: insertado en Access pero PostgreSQL no se actualizó."
                continue
            }

            Log-Sync "[SYNC] $serie-$numPedido insertado en PedVentas, LineasPedVentas e IvaLineasPedVentas [OK]."
            $count++
        }
    }
    finally {
        if ($cn -and $cn.State -ne 0) {
            $cn.Close()
        }
    }

    return $count
}

if ($Loop) {
    Log-Sync "Sincronizador iniciado. Base Access: $pathMdb"

    while ($true) {
        try {
            [void](Sincronizar-Pedidos-UnaVez)
        }
        catch {
            Log-Sync "[ERROR] $($_.Exception.Message)"
            Log-Error "$($_.Exception.Message)"
        }

        Start-Sleep -Seconds $IntervaloSegundos
    }
}
else {
    try {
        $n = Sincronizar-Pedidos-UnaVez
        Write-Host "Sincronización ejecutada. $n pedido(s) procesado(s)." -ForegroundColor Green
    }
    catch {
        Log-Sync "[ERROR] $($_.Exception.Message)"
        Log-Error "$($_.Exception.Message)"
        exit 1
    }
}
