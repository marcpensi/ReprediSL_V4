param(
    [string]$ConfigFile = "C:\Pensi\PsForce\config.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigFile)) {
    throw "No existe el fichero de configuracion: $ConfigFile"
}

$Config = Get-Content -LiteralPath $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json

# PostgreSQL
$env:PGHOST = [string]$Config.PostgreSQL.Host
$env:PGPORT = [string]$Config.PostgreSQL.Port
$env:PGCLIENTENCODING = [string]$Config.PostgreSQL.ClientEncoding

$pgDatabase = [string]$Config.PostgreSQL.Database
$pgUser = [string]$Config.PostgreSQL.User
$pgBinPath = [string]$Config.PostgreSQL.BinPath
$psqlExe = Join-Path $pgBinPath "psql.exe"

# Paths principales
$appPensiPath = [string]$Config.Paths.PensiPath
$projectName = [string]$Config.Paths.ProjectName
$projectPath = [string]$Config.Paths.ProjectPath
$logsPath = [string]$Config.Paths.LogsPath
$scriptsPath = [string]$Config.Paths.ScriptsPath
$syncPath = [string]$Config.Paths.SyncPath
$apiPath = [string]$Config.Paths.ApiPath
$caddyPath = [string]$Config.Paths.CaddyPath
$configPath = [string]$Config.Paths.ConfigPath
$backupPath = [string]$Config.Paths.BackupPath
$accessDatabase = [string]$Config.Paths.AccessDatabase

# Crear directorios operativos si faltan
@($logsPath, $configPath, $backupPath) | ForEach-Object {
    if ($_ -and -not (Test-Path -LiteralPath $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

# Logs
$logSyncFile = Join-Path $logsPath ([string]$Config.Logs.SyncProgress)
$logErrorsFile = Join-Path $logsPath ([string]$Config.Logs.Errors)
$logPendingFile = Join-Path $logsPath ([string]$Config.Logs.PendingOrders)
$logDiscardedFile = Join-Path $logsPath ([string]$Config.Logs.DiscardedOrders)

# Añadir PostgreSQL BIN al PATH de esta sesion
if ($pgBinPath -and ($env:Path -notlike "*$pgBinPath*")) {
    $env:Path = "$pgBinPath;$env:Path"
}
