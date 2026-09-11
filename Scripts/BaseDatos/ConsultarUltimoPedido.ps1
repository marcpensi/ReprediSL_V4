param(
    [string]$DbMdb = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}

if (-not $DbMdb) {
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
    if ($cfg) {
        $DbMdb = $cfg.AccessDbPath
    }
}

if (-not $DbMdb -or -not (Test-Path -LiteralPath $DbMdb)) {
    throw "No se ha encontrado el archivo gestion.mdb en: $DbMdb"
}

Write-Host "Consultando pedidos en: $DbMdb" -ForegroundColor Cyan

$cn = New-Object -ComObject ADODB.Connection
$cn.Open("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$DbMdb")
$rs = $cn.Execute("SELECT TOP 5 IdEmpresa, Ejercicio, Serie, NumPedido, Fecha, IdCliente, Total, Canal FROM PedidosCab ORDER BY Fecha DESC")

while (-not $rs.EOF) {
    $s = $rs.Fields.Item("Serie").Value
    $n = $rs.Fields.Item("NumPedido").Value
    $c = $rs.Fields.Item("IdCliente").Value
    $t = $rs.Fields.Item("Total").Value
    $f = $rs.Fields.Item("Fecha").Value
    Write-Host "Serie: '$s' | NumPedido: $n | Cliente: $c | Total: $t | Fecha: $f"
    $rs.MoveNext()
}

$cn.Close()
