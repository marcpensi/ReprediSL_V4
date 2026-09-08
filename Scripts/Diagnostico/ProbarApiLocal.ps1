$endpoints = @('clientes', 'vendedores', 'tarifas', 'precios', 'uventas')

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " VERIFICANDO RESPUESTA DE API POSTGREST LOCAL (127.0.0.1:3000)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

foreach ($ep in $endpoints) {
    Write-Host "--> Probando endpoint: /$ep" -ForegroundColor Yellow
    try {
        $res = Invoke-RestMethod "http://127.0.0.1:3000/$ep?limit=2" -TimeoutSec 3
        $json = $res | ConvertTo-Json -Compress
        if ($json.Length -gt 120) { $json = $json.Substring(0, 120) + '...' }
        Write-Host "    [OK API] Respuesta ($ep): $json" -ForegroundColor Green
    } catch {
        Write-Host "    [AVISO] /$ep : $($_.Exception.Message)" -ForegroundColor Yellow
    }
    Write-Host ""
}
