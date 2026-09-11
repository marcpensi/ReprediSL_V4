# Repara-pgAdmin-4.ps1
$ErrorActionPreference = "Stop"

$pgAdminDir = Join-Path $env:APPDATA "pgAdmin"
$dbFile     = Join-Path $pgAdminDir "pgadmin4.db"
$timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir  = Join-Path $env:APPDATA "pgAdmin_backup_$timestamp"
$dbOld      = "pgadmin4.db.old_$timestamp"

Write-Host ""
Write-Host "==============================================="
Write-Host "  REPARA pgAdmin 4"
Write-Host "==============================================="
Write-Host ""

Write-Host "[1/4] Cerrando pgAdmin 4 y procesos Python..."
Get-Process pgAdmin4,python -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "      OK"

Write-Host "[2/4] Comprobando configuracion..."
if (-not (Test-Path $pgAdminDir)) {
    Write-Host "      No existe: $pgAdminDir"
    Write-Host "      No hay configuracion de pgAdmin que reparar."
    Pause
    exit 0
}
Write-Host "      Encontrada: $pgAdminDir"

Write-Host "[3/4] Creando copia de seguridad..."
Copy-Item $pgAdminDir $backupDir -Recurse -Force
Write-Host "      Backup: $backupDir"

Write-Host "[4/4] Renovando base interna de pgAdmin..."
if (Test-Path $dbFile) {
    Rename-Item $dbFile $dbOld
    Write-Host "      Renombrado a: $dbOld"
} else {
    Write-Host "      No existe pgadmin4.db; no es necesario renombrarlo."
}

Write-Host ""
Write-Host "==============================================="
Write-Host "  REPARACION TERMINADA"
Write-Host "==============================================="
Write-Host ""
Write-Host "Ahora abre pgAdmin 4 de nuevo."
Write-Host ""
Write-Host "Copia de seguridad:"
Write-Host "  $backupDir"
Write-Host ""
Pause
