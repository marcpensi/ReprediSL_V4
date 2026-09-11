param(
    [string]$ConfigFile = ""
)

$ErrorActionPreference = "Stop"

# Redirigir a Scripts/Despliegue/Load-PsForceConfig.ps1
$loaderScript = Join-Path $PSScriptRoot "Despliegue\Load-PsForceConfig.ps1"
if (-not (Test-Path -LiteralPath $loaderScript)) {
    $loaderScript = Join-Path (Split-Path -Parent $PSScriptRoot) "Scripts\Despliegue\Load-PsForceConfig.ps1"
}

return & $loaderScript -ConfigFile $ConfigFile
