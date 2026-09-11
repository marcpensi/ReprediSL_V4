param(
    [string]$ConfigFile = ""
)

$ErrorActionPreference = "Stop"

# 1. Resolver ruta a config.json dinamicamente sin hardcode
$resolvedConfig = ""

if ($ConfigFile -and (Test-Path -LiteralPath $ConfigFile)) {
    $resolvedConfig = (Resolve-Path -LiteralPath $ConfigFile).Path
}
elseif ($env:PSFORCE_CONFIG -and (Test-Path -LiteralPath $env:PSFORCE_CONFIG)) {
    $resolvedConfig = (Resolve-Path -LiteralPath $env:PSFORCE_CONFIG).Path
}
else {
    # Buscar recursivamente hacia arriba desde el script actual
    $curr = $PSScriptRoot
    if (-not $curr) {
        $curr = Split-Path -Parent $MyInvocation.MyCommand.Definition
    }
    while ($curr) {
        $candidate = Join-Path $curr "config.json"
        if (Test-Path -LiteralPath $candidate) {
            $resolvedConfig = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
        $parent = Split-Path -Parent $curr
        if ($parent -eq $curr) { break }
        $curr = $parent
    }
}

if (-not $resolvedConfig) {
    $defaultCandidate = "C:\Pensi\PsForce\config.json"
    if (Test-Path -LiteralPath $defaultCandidate) {
        $resolvedConfig = $defaultCandidate
    }
}

if (-not $resolvedConfig -or -not (Test-Path -LiteralPath $resolvedConfig)) {
    throw "No se ha encontrado el archivo config.json en ninguna ruta de búsqueda."
}

# 2. Leer y parsear config.json
$jsonContent = Get-Content -LiteralPath $resolvedConfig -Raw -Encoding UTF8
$rawConfig = ConvertFrom-Json -InputObject $jsonContent

$projectRoot = Split-Path -Parent $resolvedConfig

# 3. Parametros y rutas derivadas
$pensiPath    = if ($rawConfig.PensiPath) { [string]$rawConfig.PensiPath } else { "C:\Pensi" }
$projectName  = if ($rawConfig.ProjectName) { [string]$rawConfig.ProjectName } else { "PsForce" }

$psGestFolder = if ($rawConfig.PsGest.FolderName) { [string]$rawConfig.PsGest.FolderName } else { "PsGestw" }
$empresa      = if ($rawConfig.PsGest.Empresa) { [int]$rawConfig.PsGest.Empresa } else { 1 }
$ejercicio    = if ($rawConfig.PsGest.Ejercicio) { [int]$rawConfig.PsGest.Ejercicio } else { 2026 }
$dbFile       = if ($rawConfig.PsGest.DatabaseFile) { [string]$rawConfig.PsGest.DatabaseFile } else { "gestion.mdb" }

$empresaFmt   = "e{0:D3}{1}" -f $empresa, $ejercicio
$accessDbPath = Join-Path (Join-Path (Join-Path $pensiPath $psGestFolder) $empresaFmt) $dbFile

# Fallback si no existe en la ruta de produccion (p. ej. pruebas en dev)
if (-not (Test-Path -LiteralPath $accessDbPath)) {
    $testCandidates = @(
        (Join-Path $projectRoot "Test\$empresaFmt\$dbFile"),
        (Join-Path $projectRoot "Test\$dbFile"),
        (Join-Path $projectRoot "src\Access\$empresaFmt\$dbFile"),
        (Join-Path $projectRoot "src\Access\$dbFile")
    )
    foreach ($cand in $testCandidates) {
        if (Test-Path -LiteralPath $cand) {
            $accessDbPath = (Resolve-Path -LiteralPath $cand).Path
            break
        }
    }
}

$logsPath    = Join-Path $projectRoot "Logs"
$scriptsPath = Join-Path $projectRoot "Scripts"
$syncPath    = Join-Path $projectRoot "Sync"
$caddyPath   = Join-Path $projectRoot "Caddy"
$backupPath  = Join-Path $projectRoot "Backup"

if (-not (Test-Path -LiteralPath $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
}

# 4. PostgreSQL
$pgHost           = if ($rawConfig.PostgreSQL.Host) { [string]$rawConfig.PostgreSQL.Host } else { "localhost" }
$pgPort           = if ($rawConfig.PostgreSQL.Port) { [int]$rawConfig.PostgreSQL.Port } else { 5433 }
$pgDatabase       = if ($rawConfig.PostgreSQL.Database) { [string]$rawConfig.PostgreSQL.Database } else { "repredisl_api" }
$pgUser           = if ($rawConfig.PostgreSQL.User) { [string]$rawConfig.PostgreSQL.User } else { "postgres" }
$pgClientEncoding = if ($rawConfig.PostgreSQL.ClientEncoding) { [string]$rawConfig.PostgreSQL.ClientEncoding } else { "UTF8" }
$pgServiceName    = if ($rawConfig.PostgreSQL.ServiceName) { [string]$rawConfig.PostgreSQL.ServiceName } else { "postgresql-x64-17" }

# Buscar binarios psql dinamicamente
$psqlExe = ""
$cmdPsql = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmdPsql) {
    $psqlExe = $cmdPsql.Source
}
else {
    $pgVersions = Get-ChildItem "C:\Program Files\PostgreSQL" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending
    foreach ($v in $pgVersions) {
        $candPsql = Join-Path $v.FullName "bin\psql.exe"
        if (Test-Path -LiteralPath $candPsql) {
            $psqlExe = $candPsql
            break
        }
    }
}

# 5. Exportar variables de entorno de conexion para procesos secundarios
$env:PGHOST           = $pgHost
$env:PGPORT           = "$pgPort"
$env:PGDATABASE       = $pgDatabase
$env:PGUSER           = $pgUser
$env:PGCLIENTENCODING = $pgClientEncoding
$env:PSFORCE_PROJECT_ROOT = $projectRoot
$env:PSFORCE_LOGS_DIR = $logsPath
$env:PSFORCE_CONFIG   = $resolvedConfig

# Asignar PGPASSWORD en memoria a partir de PGREPREAPIPWD si existe
if ($env:PGREPREAPIPWD) {
    $env:PGPASSWORD = $env:PGREPREAPIPWD
}

# 6. Empaquetar objeto resultado
$PsForceConfig = [PSCustomObject]@{
    ConfigFilePath   = $resolvedConfig
    ProjectRoot      = $projectRoot
    PensiPath        = $pensiPath
    ProjectName      = $projectName
    AccessDbPath     = $accessDbPath
    LogsPath         = $logsPath
    ScriptsPath      = $scriptsPath
    SyncPath         = $syncPath
    CaddyPath        = $caddyPath
    BackupPath       = $backupPath
    PostgreSQL       = [PSCustomObject]@{
        Host           = $pgHost
        Port           = $pgPort
        Database       = $pgDatabase
        User           = $pgUser
        ClientEncoding = $pgClientEncoding
        ServiceName    = $pgServiceName
        PsqlExe        = $psqlExe
    }
    PostgREST        = [PSCustomObject]@{
        Port = if ($rawConfig.PostgREST.Port) { [int]$rawConfig.PostgREST.Port } else { 3000 }
    }
    Caddy            = [PSCustomObject]@{
        HttpPort  = if ($rawConfig.Caddy.HttpPort) { [int]$rawConfig.Caddy.HttpPort } else { 80 }
        HttpsPort = if ($rawConfig.Caddy.HttpsPort) { [int]$rawConfig.Caddy.HttpsPort } else { 443 }
    }
}

# Retornar objeto si se llama como funcion/expresion
return $PsForceConfig
