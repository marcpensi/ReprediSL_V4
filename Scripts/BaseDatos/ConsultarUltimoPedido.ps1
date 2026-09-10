param()

$mdb = "D:\programacio\repredi\ReprediSL_V4\src\Access\E0012026\gestion.mdb"
$cn = New-Object -ComObject ADODB.Connection
$cn.Open("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$mdb")
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
