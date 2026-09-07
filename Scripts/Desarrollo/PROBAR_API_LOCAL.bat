\
    @echo off
    echo Probando PostgREST local...
    powershell -NoProfile -Command "try { Invoke-RestMethod 'http://127.0.0.1:3000/clientes?limit=5' | ConvertTo-Json -Depth 5 } catch { Write-Host $_.Exception.Message -ForegroundColor Red }"
    pause
